using LLVMSharp;

namespace Eokas;

public class Symbol
{
    public Scope scope;
    public LLVMTypeRef type;
    public LLVMValueRef value;
}

public class Schema
{
    public Scope scope;
    public LLVMTypeRef type;
    public List<LLVMTypeRef> generics = new List<LLVMTypeRef>();
    
    public LLVMTypeRef ResolveGenerics(List<LLVMTypeRef> args)
    {
        for(size_t index = 0; index < this.generics.Count; index++)
        {
            var gen = this.generics[index];
            var arg = args[index];
            Schema.FillOpaqueStructType(gen, arg);
        }
        return this.type;
    }
		
    static void FillOpaqueStructType(LLVMTypeRef opaqueT, LLVMTypeRef structT)
    {
        var elementTypes = structT.GetStructElementTypes();
        opaqueT.StructSetBody(elementTypes, false);
    }
}

public class Scope
{
    public Scope parent;
    public LLVMValueRef func;
    public List<Scope> children = new List<Scope>();

    public Dictionary<string, Symbol> symbols = new Dictionary<string, Symbol>();
    public Dictionary<string, Schema> schemas = new Dictionary<string, Schema>();

    public Scope(Scope parent, LLVMValueRef func)
    {
        this.parent = parent;
        this.func = func;
    }

    public Scope AddChild(LLVMValueRef func)
    {
        var child = new Scope(this, func.Pointer == IntPtr.Zero ? this.func : func);
        this.children.Add(child);
        return child;
    }

    public bool AddSymbol(String name, LLVMTypeRef type)
    {
        var symbol = new Symbol();
        symbol.scope = this;
        symbol.type = type;
        symbol.value = default;
        
        if (!this.symbols.ContainsKey(name))
        {
            this.symbols.Add(name, symbol);
            return true;
        }

        return false;
    }

    public bool AddSymbol(String name, LLVMValueRef expr)
    {
        var symbol = new Symbol();
        symbol.scope = this;
        symbol.type = expr.TypeOf();
        symbol.value = expr;

        if (!this.symbols.ContainsKey(name))
        {
            this.symbols.Add(name, symbol);
            return true;
        }

        return false;
    }
    
    public Symbol GetSymbol(String name, bool lookup)
    {
        if(lookup)
        {
            for (var scope = this; scope != null; scope = scope.parent)
            {
                if (scope.symbols.TryGetValue(name, out var symbol))
                    return symbol;
            }
            return null;
        }
        else
        {
            Symbol symbol = null;
            this.symbols.TryGetValue(name, out symbol);
            return symbol;
        }
    }
    
    public bool AddSchema(String name, LLVMTypeRef type)
    {
        var schema = new Schema();
        schema.scope = this;
        schema.type = type;

        if (!this.schemas.ContainsKey(name))
        {
            this.schemas.Add(name, schema);
            return true;
        }

        return false;
    }
    
    public Schema GetSchema(String name, bool lookup)
    {
        if(lookup)
        {
            for (var scope = this; scope != null; scope = scope.parent)
            {
                if (scope.schemas.TryGetValue(name, out var schema))
                    return schema;
            }
            return null;
        }
        else
        {
            Schema schema = null;
            this.schemas.TryGetValue(name, out schema);
            return schema;
        }
    }
}
