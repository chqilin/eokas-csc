
using LLVMSharp;

namespace Eokas.Modules;

public class Coder : Module
{
    private Scope scope;
    
    private LLVMBuilderRef builder;
    
    public Coder(string name)
        : base(name)
    {
        this.scope = new Scope(null, this.entry);
        this.builder = LLVM.CreateBuilder();
    }

    public bool Encode(AstNodeModule node)
    {
        this.PushScope(default);
        {
            var entry = LLVM.AppendBasicBlock(this.scope.func, "entry");
            LLVM.PositionBuilderAtEnd(this.builder, entry);
            
            foreach (var item in node.symbols)
            {
                var name = item.Key;
                var symbol = item.Value;
                
                var expr = this.EncodeExpr(symbol.value);
                if (!this.scope.AddSymbol(name, expr))
                {
                    // TODO
                    return false;
                }

                if (symbol.isPublic)
                {
                    var type = LLVM.TypeOf(expr);
                    var ptr = LLVM.AddGlobal(this.handle, type, name);
                    LLVM.BuildStore(this.builder, expr, ptr);
                }
            }
            
            var lastOp = LLVM.GetInsertBlock(this.builder).GetLastInstruction();
            var isTerminator = lastOp.IsATerminatorInst();
            if(isTerminator.Pointer == IntPtr.Zero)
            {
                var retT = this.entry.TypeOf().GetReturnType();
                if (retT.TypeKind == LLVMTypeKind.LLVMVoidTypeKind)
                    LLVM.BuildRetVoid(this.builder);
                else
                    LLVM.BuildRet(this.builder, Model.GetDefaultValue(retT));
            }
        }
        this.PopScope();
    }

    private void PushScope(LLVMValueRef func)
    {
        this.scope = this.scope.AddChild(func);
    }

    private void PopScope()
    {
        this.scope = this.scope.parent;
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