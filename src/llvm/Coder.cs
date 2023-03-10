using LLVMSharp;

namespace Eokas.llvm;

public class Coder
{
    private LLVMContextRef context;
    private LLVMModuleRef module;
    private LLVMBuilderRef builder;

    private LLVMTypeRef type_i8;
    private LLVMTypeRef type_i16;
    private LLVMTypeRef type_i32;
    private LLVMTypeRef type_i64;
    private LLVMTypeRef type_f32;
    private LLVMTypeRef type_f64;

    public Coder(LLVMContextRef context)
    {
        this.context = context;
    }

    public LLVMModuleRef Encode(AstNodeModule module)
    {
        this.type_i8 = LLVM.Int8Type();
        this.type_i16 = LLVM.Int16Type();
        this.type_i32 = LLVM.Int32Type();
        this.type_i64 = LLVM.Int64Type();
        this.type_f32 = LLVM.FloatType();
        this.type_f64 = LLVM.DoubleType();

        
        return this.EncodeModule(module);
    }

    private LLVMModuleRef EncodeModule(AstNodeModule node)
    {
        this.module = LLVM.ModuleCreateWithName(node.name);
        this.builder = LLVM.CreateBuilder();

        foreach (var item in node.symbols)
        {
            var name = item.Key;
            var symbol  = item.Value;
            var expr = this.EncodeExpr(symbol.value);
            var type = LLVM.TypeOf(expr);
            var ptr = LLVM.AddGlobal(this.module, type, name);
            LLVM.BuildStore(this.builder, expr, ptr);
        }

        return this.module;
    }

    private LLVMTypeRef EncodeType(AstNodeType node)
    {
        return default;
    }

    private LLVMValueRef EncodeExpr(AstNodeExpr node)
    {
        return default;
    }

    private bool EncodeStmt(AstNodeStmt node)
    {
        return false;
    }
    
    
}
