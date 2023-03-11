using LLVMSharp;

namespace Eokas;

public class Module
{
    public string name { get; private set; }
    public LLVMModuleRef handle { get; private set; }
    public LLVMValueRef entry { get; private set; }
        
    public Dictionary<string, LLVMValueRef> symbols = new Dictionary<string, LLVMValueRef>();
    public  Dictionary<string, LLVMTypeRef> schemas = new Dictionary<string, LLVMTypeRef>();

    public Module(string name)
    {
        this.name = name;
        this.handle = LLVM.ModuleCreateWithName(name);
        this.entry = this.DeclareFunction("@main", Model.TyVoid, new LLVMTypeRef[] { }, false);
    }

    ~Module()
    {
        LLVM.DisposeModule(this.handle);
    }

    public void Dump()
    {
        LLVM.DumpModule(this.handle);
    }

    public LLVMValueRef DeclareFunction(string name, LLVMTypeRef ret, LLVMTypeRef[] args, bool varg)
    {
        return Model.DeclareFunction(this.handle, name, ret, args, varg);
    }

    public LLVMValueRef DefineFunction(string name, LLVMTypeRef ret, LLVMTypeRef[] args, bool varg,
        Action<LLVMModuleRef, LLVMValueRef, LLVMBuilderRef> body)
    {
        return Model.DefineFunction(this.handle, name, ret, args, varg, body);
    }
}