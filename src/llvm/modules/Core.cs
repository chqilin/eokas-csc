using LLVMSharp;

namespace Eokas.Modules;

public class Core : Module
{
    public Core(string name)
        : base(name)
    {
        this.DeclareFunction("molloc", Model.TyBytePtr, new[] { Model.TyI64 }, false);
        this.DeclareFunction("free", Model.TyVoid, new[] { Model.TyBytePtr }, false);
        
        this.DeclareFunction("printf", Model.TyI32, new[] { Model.TyBytePtr }, true);
        this.DeclareFunction("sprintf", Model.TyI32, new[] { Model.TyBytePtr, Model.TyBytePtr }, true);
        
        this.DeclareFunction("strlen", Model.TyI32, new[] { Model.TyBytePtr }, true);
    }
}