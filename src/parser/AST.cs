namespace Eokas;

public enum AstCategory
{
	NONE,
	
	MODULE, USING,

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
	
public enum AstBinaryOper
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
	
public enum AstUnaryOper
{
	POS = 900, NEG, FLIP, SIZE_OF, TYPE_OF,
	NOT = 1000,
	MAX_LEVEL = 1100,
	UNKNOWN = 0x7FFFFFFF
}
	
public struct AstPosition
{
	int row;
	int col;
    
	void set(int r, int c)
	{
		this.row = r;
		this.col = c;
	}
}


public class AstNode
{
	public AstCategory category;
	public AstNode? parent;

	public AstNode(AstCategory category, AstNode parent)
	{
		this.category = category;
		this.parent = parent;
	}
}

public class AstNodeModule : AstNode
{
	public String name = "";
	public Dictionary<String, AstNodeUsing> usings = new Dictionary<string, AstNodeUsing>();
	public Dictionary<String, AstNodeSymbolDef> symbols = new Dictionary<string, AstNodeSymbolDef>();
	public Dictionary<String, AstNodeType> types = new Dictionary<string, AstNodeType>();

	public AstNodeModule(AstNode parent)
		: base(AstCategory.MODULE, parent)
	{ }
}

public class AstNodeUsing : AstNode
{
	public String name = "";
	public String path = "";

	public AstNodeUsing(AstNode parent)
		: base(AstCategory.USING, parent)
	{ }
}

public class AstNodeType : AstNode
{
	public String name = "";
	public List<AstNodeType> args = new List<AstNodeType>();

	public AstNodeType(AstNode parent)
		: base(AstCategory.TYPE, parent)
	{ }
}

public class AstNodeExpr : AstNode
{
	public AstNodeExpr(AstCategory category, AstNode parent)
		: base(category, parent)
	{ }
}

public class AstNodeStmt : AstNode
{
	public AstNodeStmt(AstCategory category, AstNode parent)
		: base(category, parent)
	{ }
}

public class AstNodeFuncDef : AstNodeExpr
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
	{ }

	public Arg? AddArg(String name)
	{
		if (this.GetArg(name) != null)
			return null;
		
		var arg = new Arg();
		arg.name = name;
		this.args.Add(arg);
		
		return arg;
	}

	public Arg? GetArg(String name)
	{
		foreach (var arg in this.args)
		{
			if (arg.name == name)
				return arg;
		}

		return null;
	}
}

public class AstNodeFuncRef : AstNodeExpr
{
	public AstNodeExpr func = null;
	public List<AstNodeExpr> args = new List<AstNodeExpr>();

	public AstNodeFuncRef(AstNode parent)
		:base(AstCategory.FUNC_REF, parent)
	{ }
}

public class AstNodeSymbolDef : AstNodeStmt
{
	public String name = "";
	public AstNodeType type = null;
	public AstNodeExpr value = null;
	public bool isPublic = false;
	public bool isVariable = false;

	public AstNodeSymbolDef(AstNode parent)
		:base(AstCategory.SYMBOL_DEF, parent)
	{ }
}

public class AstNodeSymbolRef : AstNodeExpr
{
	public String name = "";

	public AstNodeSymbolRef(AstNode parent)
		:base(AstCategory.SYMBOL_REF, parent)
	{ }
}

public class AstNodeExprTrinary : AstNodeExpr
{
	public AstNodeExpr cond = null;
	public AstNodeExpr branch_true = null;
	public AstNodeExpr branch_false = null;

	public AstNodeExprTrinary(AstNode parent)
		: base(AstCategory.EXPR_TRINARY, parent)
	{ }
}

public class AstNodeExprBinary : AstNodeExpr
{
	public AstBinaryOper op = Eokas.AstBinaryOper.UNKNOWN;
	public AstNodeExpr left = null;
	public AstNodeExpr right = null;

	public AstNodeExprBinary(AstNode parent)
		:base(AstCategory.EXPR_BINARY, parent)
	{ }
}

public class AstNodeExprUnary : AstNodeExpr
{
	public AstUnaryOper op = AstUnaryOper.UNKNOWN;
	public AstNodeExpr right = null;

	public AstNodeExprUnary(AstNode parent)
		:base(AstCategory.EXPR_UNARY, parent)
	{ }
}

public class AstNodeLiteralInt : AstNodeExpr
{
	public long value = 0;

	public AstNodeLiteralInt(AstNode parent)
		:base(AstCategory.LITERAL_INT, parent)
	{ }
}

public class AstNodeLiteralFloat : AstNodeExpr
{
	public double value = 0;

	public AstNodeLiteralFloat(AstNode parent)
		:base(AstCategory.LITERAL_FLOAT, parent)
	{ }
}

public class AstNodeLiteralBool : AstNodeExpr
{
	public bool value = false;

	public AstNodeLiteralBool(AstNode parent)
		:base(AstCategory.LITERAL_BOOL, parent)
	{ }
}

public class AstNodeLiteralString : AstNodeExpr
{
	public String value = "";

	public AstNodeLiteralString(AstNode parent)
		:base(AstCategory.LITERAL_STRING, parent)
	{ }
}

public class AstNodeArrayDef : AstNodeExpr
{
	public List<AstNodeExpr> elements = new List<AstNodeExpr>();

	public AstNodeArrayDef(AstNode parent)
		: base(AstCategory.ARRAY_DEF, parent)
	{ }
}

public class AstNodeArrayRef : AstNodeExpr
{
	public AstNodeExpr obj = null;
	public AstNodeExpr key = null;

	public AstNodeArrayRef(AstNode parent)
		: base(AstCategory.ARRAY_REF, parent)
	{ }
}

public class AstNodeObjectDef : AstNodeExpr
{
	public AstNodeType type = null;
	public Dictionary<String, AstNodeExpr> members = new Dictionary<string, AstNodeExpr>();

	public AstNodeObjectDef(AstNode parent)
		: base(AstCategory.OBJECT_DEF, parent)
	{ }
}

public class AstNodeObjectRef : AstNodeExpr
{
	public AstNodeExpr obj = null;
	public String key = "";

	public AstNodeObjectRef(AstNode parent)
		: base(AstCategory.OBJECT_REF, parent)
	{ }
}

public class AstNodeStructDef : AstNodeStmt
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
	{ }

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

public class AstNodeEnumDef : AstNodeStmt
{
	public String name = "";
	public Dictionary<String, int> members = new Dictionary<string, int>();

	public AstNodeEnumDef(AstNode parent)
		: base(AstCategory.ENUM_DEF, parent)
	{ }
}

public class AstNodeProcDef : AstNodeStmt
{
	public String name = "";
	public AstNodeType type = null;
	public Dictionary<String, AstNodeType> args = new Dictionary<string, AstNodeType>();

	public AstNodeProcDef(AstNode parent)
		: base(AstCategory.PROC_DEF, parent)
	{ }
}

public class AstNodeReturn : AstNodeStmt
{
	public AstNodeExpr value = null;

	public AstNodeReturn(AstNode parent)
		:base(AstCategory.RETURN, parent)
	{ }
}

public class AstNodeIf : AstNodeStmt
{
	public AstNodeExpr cond = null;
	public AstNodeStmt branch_true = null;
	public AstNodeStmt branch_false = null;

	public AstNodeIf(AstNode parent)
		:base(AstCategory.IF, parent)
	{ }
}

public class AstNodeLoop : AstNodeStmt
{
	public AstNodeStmt init = null;
	public AstNodeExpr cond = null;
	public AstNodeStmt step = null;
	public AstNodeStmt body = null;

	public AstNodeLoop(AstNode parent)
		:base(AstCategory.LOOP, parent)
	{ }
}

public class AstNodeBreak : AstNodeStmt
{
	public AstNodeBreak(AstNode parent)
		:base(AstCategory.BREAK, parent)
	{ }
}

public class AstNodeContinue : AstNodeStmt
{
	public AstNodeContinue(AstNode parent)
		: base(AstCategory.CONTINUE, parent)
	{ }
}

public class AstNodeBlock : AstNodeStmt
{
	public List<AstNodeStmt> stmts = new List<AstNodeStmt>();

	public AstNodeBlock(AstNode parent)
		: base(AstCategory.BLOCK, parent)
	{ }
}

public class AstNodeAssign : AstNodeStmt
{
	public AstNodeExpr left = null;
	public AstNodeExpr right = null;

	public AstNodeAssign(AstNode parent)
		:base(AstCategory.ASSIGN, parent)
	{ }
}

public class AstNodeInvoke : AstNodeStmt
{
	public AstNodeFuncRef expr = null;

	public AstNodeInvoke(AstNode parent)
		: base(AstCategory.INVOKE, parent)
	{ }
}
