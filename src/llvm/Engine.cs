
namespace Eokas;

using LLVMSharp;

public class LLVMEngine
{
    public static bool JIT(AstNodeModule astModule)
    {
	    LLVM.LinkInMCJIT();
        LLVM.InitializeX86TargetInfo();
        LLVM.InitializeX86Target();
        LLVM.InitializeX86TargetMC();
        
        LLVMModuleRef module = LLVM.ModuleCreateWithName("eokas-main");
        LLVMBuilderRef builder = LLVM.CreateBuilder();
        
        LLVM.DumpModule(module);
            
        if (LLVM.CreateExecutionEngineForModule(out var engine, module, out var errorMessage).Value == 1)
        {
	        Console.WriteLine(errorMessage);
	        return false;
        }
        
        Console.WriteLine("---------------- JIT RUN ----------------");
        var func = LLVM.GetNamedFunction(module, "main");
        LLVMGenericValueRef[] args = new LLVMGenericValueRef[] { };
        LLVMGenericValueRef retval = LLVM.RunFunction(engine, func, args);
        Console.WriteLine("RET: {0} \n", retval.Pointer);
        Console.WriteLine("---------------- JIT END ----------------");
        
        LLVM.DisposeModule(module);
        
        return true;
    }

    public static bool AOT(AstNodeModule astModule)
    {
	    LLVM.InitializeAllTargetInfos();
	    LLVM.InitializeAllTargets();
	    LLVM.InitializeAllTargetMCs();
	    LLVM.InitializeAllAsmParsers();
	    LLVM.InitializeAllAsmPrinters();
	    
	    LLVMModuleRef module = LLVM.ModuleCreateWithName("eokas-main");
	    LLVMBuilderRef builder = LLVM.CreateBuilder();
	    
	    LLVM.DumpModule(module);
	    
	    var targetTriple = LLVM.GetDefaultTargetTriple();
	    if (LLVM.GetTargetFromTriple(targetTriple.ToString(), out var target, out var error).Value == 1)
	    {
		    return false;
	    }
	    
	    var cpu = "generic";
	    var features = "";
	    LLVMCodeGenOptLevel level = LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault;
	    LLVMRelocMode rm = LLVMRelocMode.LLVMRelocDefault;
	    LLVMCodeModel cm = LLVMCodeModel.LLVMCodeModelDefault;
	    var targetMachine = LLVM.CreateTargetMachine(target, targetTriple.ToString(),
		    cpu, features, level, rm, cm);
	    
	    var dataLayout = LLVM.CreateTargetDataLayout(targetMachine);
	    LLVM.SetDataLayout(module, dataLayout.ToString());
	    LLVM.SetTarget(module, targetTriple.ToString());

	    
	    
	    return true;
    }

    private static LLVMPassManagerRef SetupPassManager(LLVMModuleRef module)
    {
	    // Create a function pass manager for this engine
	    LLVMPassManagerRef passManager = LLVM.CreateFunctionPassManagerForModule(module);

	    // Set up the optimizer pipeline.  Start with registering info about how the
	    // target lays out data structures.
	    // LLVM.DisposeTargetData(LLVM.GetExecutionEngineTargetData(engine));

	    // Provide basic AliasAnalysis support for GVN.
	    LLVM.AddBasicAliasAnalysisPass(passManager);

	    // Promote allocas to registers.
	    LLVM.AddPromoteMemoryToRegisterPass(passManager);

	    // Do simple "peephole" optimizations and bit-twiddling optzns.
	    LLVM.AddInstructionCombiningPass(passManager);

	    // Reassociate expressions.
	    LLVM.AddReassociatePass(passManager);

	    // Eliminate Common SubExpressions.
	    LLVM.AddGVNPass(passManager);

	    // Simplify the control flow graph (deleting unreachable blocks, etc).
	    LLVM.AddCFGSimplificationPass(passManager);

	    LLVM.InitializeFunctionPassManager(passManager);

	    return passManager;
    }
}
