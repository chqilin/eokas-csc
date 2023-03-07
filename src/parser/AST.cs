namespace Eokas;

enum AstCategory
{
	NONE,

	PROGRAM,

	MODULE, IMPORT, EXPORT,

	TYPE,
		
	FUNC_DEF, FUNC_REF,
	SYMBOL_DEF, SYMBOL_REF,

	EXPR_TRINARY, EXPR_BINARY, EXPR_UNARY,
	LITERAL_INT, LITERAL_FLOAT, LITERAL_BOOL, LITERAL_STRING,
	ARRAY_DEF, ARRAY_REF,
	OBJECT_DEF, OBJECT_REF,

	STRUCT_DEF, ENUM_DEF, PROC_DEF,

	RETURN, IF, LOOP, BREAK, CONTINUE, BLOCK, ASSIGN, INVOKE,
}
	
enum AstBinaryOper
{
	OR = 100,
	AND = 200,
	EQ = 300, NE, LE, GE, LT, GT,
	ADD = 400, SUB,
	MUL = 500, DIV, MOD,
	BIT_AND = 600, BIT_OR, BIT_XOR, SHIFT_L, SHIFT_R,
	MAX_LEVEL = 800,
	UNKNOWN = 0x7FFFFFFF
}
	
enum AstUnaryOper
{
	POS = 900, NEG, FLIP, SIZE_OF, TYPE_OF,
	NOT = 1000,
	MAX_LEVEL = 1100,
	UNKNOWN = 0x7FFFFFFF
}
	
struct AstPosition
{
	int row;
	int col;
    
	void set(int r, int c)
	{
		this.row = r;
		this.col = c;
	}
}


class AstNode
{
	public AstCategory category;
	public AstNode parent;

	public AstNode(AstCategory category, AstNode parent)
	{
		this.category = category;
		this.parent = parent;
	}
}

class AstNodeProgram : AstNode
{
	public List<AstNodeModule> modules = new List<AstNodeModule>();
	public AstNodeModule main = null;

	public AstNodeProgram(AstNode parent)
		: base(AstCategory.PROGRAM, parent)
	{
	}
}

class AstNodeModule : AstNode
{
	public String name = "";
	public Dictionary<String, AstNodeImport> imports = new Dictionary<string, AstNodeImport>();
	public Dictionary<String, AstNodeExport> exports = new Dictionary<string, AstNodeExport>();
	public AstNodeFuncDef entry = null;

	public AstNodeModule(AstNode parent)
		: base(AstCategory.MODULE, parent)
	{
	}
}

class AstNodeImport : AstNode
{
	public String name = "";

	public AstNodeImport(AstNode parent)
		: base(AstCategory.IMPORT, parent)
	{
	}
}

class AstNodeExport : AstNode
{
	public AstNodeExport(AstCategory category, AstNode parent)
		: base(category, parent)
	{
	}
}

class AstNodeType : AstNode
{
	public String name = "";
	public List<AstNodeType> args = new List<AstNodeType>();

	public AstNodeType(AstNode parent)
		: base(AstCategory.TYPE, parent)
	{
	}
}

class AstNodeExpr : AstNode
{
	public AstNodeExpr(AstCategory category, AstNode parent)
		: base(category, parent)
	{
	}
}

class AstNodeStmt : AstNode
{
	public AstNodeStmt(AstCategory category, AstNode parent)
		: base(category, parent)
	{
	}
}

class AstNodeFuncDef : AstNodeExpr
{
	public class Arg
	{
		public String name = "";
		public AstNodeType type = null;
	};

	public AstNodeType rtype = null;
	public List<Arg> args = new List<Arg>();
	public List<AstNodeStmt> body = new List<AstNodeStmt>();

	public AstNodeFuncDef(AstNode parent)
		:base(AstCategory.FUNC_DEF, parent)
	{
	}

	public Arg AddArg(String name)
	{
		if (this.GetArg(name) != null)
			return null;
		
		var arg = new Arg();
		arg.name = name;
		this.args.Add(arg);
		
		return arg;
	}

	public Arg GetArg(String name)
	{
		foreach (var arg in this.args)
		{
			if (arg.name == name)
				return arg;
		}

		return null;
	}
}

class AstNodeFuncRef : AstNodeExpr
{
	public AstNodeExpr func = null;
	public List<AstNodeExpr> args = new List<AstNodeExpr>();

	public AstNodeFuncRef(AstNode parent)
		:base(AstCategory.FUNC_REF, parent)
	{
	}
}

class AstNodeSymbolDef : AstNodeStmt
{
	public String name = "";
	public AstNodeType type = null;
	public AstNodeExpr value = null;
	public bool variable = false;

	public AstNodeSymbolDef(AstNode parent)
		:base(AstCategory.SYMBOL_DEF, parent)
	{
	}
}

class AstNodeSymbolRef : AstNodeExpr
{
	public String name = "";

	public AstNodeSymbolRef(AstNode parent)
		:base(AstCategory.SYMBOL_REF, parent)
	{
	}
}

class AstNodeExprTrinary : AstNodeExpr
{
	public AstNodeExpr cond = null;
	public AstNodeExpr branch_true = null;
	public AstNodeExpr branch_false = null;

	public AstNodeExprTrinary(AstNode parent)
		:base(AstCategory.EXPR_TRINARY, parent)
	{
	}
}

class AstNodeExprBinary : AstNodeExpr
{
	public AstBinaryOper op = Eokas.AstBinaryOper.UNKNOWN;
	public AstNodeExpr left = null;
	public AstNodeExpr right = null;

	public AstNodeExprBinary(AstNode parent)
		:base(AstCategory.EXPR_BINARY, parent)
	{
	}
}

class AstNodeExprUnary : AstNodeExpr
{
	public AstUnaryOper op = AstUnaryOper.UNKNOWN;
	public AstNodeExpr right = null;

	public AstNodeExprUnary(AstNode parent)
		:base(AstCategory.EXPR_UNARY, parent)
	{
	}
}

class AstNodeLiteralInt : AstNodeExpr
{
	public long value = 0;

	public AstNodeLiteralInt(AstNode parent)
		:base(AstCategory.LITERAL_INT, parent)
	{
	}
}

class AstNodeLiteralFloat : AstNodeExpr
{
	public double value = 0;

	public AstNodeLiteralFloat(AstNode parent)
		:base(AstCategory.LITERAL_FLOAT, parent)
	{
	}
}

class AstNodeLiteralBool : AstNodeExpr
{
	public bool value = false;

	public AstNodeLiteralBool(AstNode parent)
		:base(AstCategory.LITERAL_BOOL, parent)
	{
	}
}

class AstNodeLiteralString : AstNodeExpr
{
	public String value = "";

	public AstNodeLiteralString(AstNode parent)
		:base(AstCategory.LITERAL_STRING, parent)
	{
	}
}

class AstNodeArrayDef : AstNodeExpr
{
	public List<AstNodeExpr> elements = new List<AstNodeExpr>();

	public AstNodeArrayDef(AstNode parent)
		: base(AstCategory.ARRAY_DEF, parent)
	{
	}
}

class AstNodeArrayRef : AstNodeExpr
{
	public AstNodeExpr obj = null;
	public AstNodeExpr key = null;

	public AstNodeArrayRef(AstNode parent)
		: base(AstCategory.ARRAY_REF, parent)
	{
	}
}

class AstNodeObjectDef : AstNodeExpr
{
	public AstNodeType type = null;
	public Dictionary<String, AstNodeExpr> members = new Dictionary<string, AstNodeExpr>();

	public AstNodeObjectDef(AstNode parent)
		: base(AstCategory.OBJECT_DEF, parent)
	{
	}
}

class AstNodeObjectRef : AstNodeExpr
{
	public AstNodeExpr obj = null;
	public String key = "";

	public AstNodeObjectRef(AstNode parent)
		: base(AstCategory.OBJECT_REF, parent)
	{
	}
}

class AstNodeStructDef : AstNodeStmt
{
	public class Member
	{
		public String name = "";
		public AstNodeType type = null;
		public AstNodeExpr value = null;
		public bool isConst = false;
	};

	public String name = "";
	public List<Member> members = new List<Member>();

	public AstNodeStructDef(AstNode parent)
		: base(AstCategory.STRUCT_DEF, parent)
	{
	}

	public Member AddMember(String name)
	{
		if (this.GetMember(name) == null)
			return null;

		var m = new Member();
		m.name = name;
		this.members.Add(m);
		
		return m;
	}

	public Member GetMember(String name)
	{
		foreach (var m in this.members)
		{
			if (m.name == name)
				return m;
		}
		return null;
	}
}

class AstNodeEnumDef : AstNodeStmt
{
	public String name = "";
	public Dictionary<String, int> members = new Dictionary<string, int>();

	public AstNodeEnumDef(AstNode parent)
		: base(AstCategory.ENUM_DEF, parent)
	{
	}
}

class AstNodeProcDef : AstNodeStmt
{
	public String name = "";
	public AstNodeType type = null;
	public Dictionary<String, AstNodeType> args = new Dictionary<string, AstNodeType>();

	public AstNodeProcDef(AstNode parent)
		: base(AstCategory.PROC_DEF, parent)
	{
	}
}

class AstNodeReturn : AstNodeStmt
{
	public AstNodeExpr value = null;

	public AstNodeReturn(AstNode parent)
		:base(AstCategory.RETURN, parent)
	{
	}
}

class AstNodeIf : AstNodeStmt
{
	public AstNodeExpr cond = null;
	public AstNodeStmt branch_true = null;
	public AstNodeStmt branch_false = null;

	public AstNodeIf(AstNode parent)
		:base(AstCategory.IF, parent)
	{
	}
}

class AstNodeLoop : AstNodeStmt
{
	public AstNodeStmt init = null;
	public AstNodeExpr cond = null;
	public AstNodeStmt step = null;
	public AstNodeStmt body = null;

	public AstNodeLoop(AstNode parent)
		:base(AstCategory.LOOP, parent)
	{
	}
}

class AstNodeBreak : AstNodeStmt
{
	public AstNodeBreak(AstNode parent)
		:base(AstCategory.BREAK, parent)
	{
	}
}

class AstNodeContinue : AstNodeStmt
{
	public AstNodeContinue(AstNode parent)
		: base(AstCategory.CONTINUE, parent)
	{
	}
}

class AstNodeBlock : AstNodeStmt
{
	public List<AstNodeStmt> stmts = new List<AstNodeStmt>();

	public AstNodeBlock(AstNode parent)
		: base(AstCategory.BLOCK, parent)
	{
	}
}

class AstNodeAssign : AstNodeStmt
{
	public AstNodeExpr left = null;
	public AstNodeExpr right = null;

	public AstNodeAssign(AstNode parent)
		:base(AstCategory.ASSIGN, parent)
	{
	}
}

class AstNodeInvoke : AstNodeStmt
{
	public AstNodeFuncRef expr = null;

	public AstNodeInvoke(AstNode parent)
		: base(AstCategory.INVOKE, parent)
	{
	}
}
