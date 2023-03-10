namespace Eokas;

public class Parser
{
	private Lexer lexer = new Lexer();

	public string error { get; private set; }

	public AstNodeModule Parse(string source)
	{
		this.Clear();
		this.lexer.Ready(source);
		this.NextToken();
		return this.ParseModule();
	}

	void Clear()
	{
		this.lexer.Clear();
		this.error = "";
	}

	AstNodeModule ParseModule()
	{
		var module = new AstNodeModule(null);

		// using
		while (this.CheckToken(Token.Type.USING, false, false))
		{
			var usingDef = this.ParseUsing(module);
			if (usingDef == null)
				return null;
			module.usings.Add(usingDef.name, usingDef);
		}
		
		// exports
		while (this.token.type != Token.Type.EOS)
		{
			var symbolDef = this.ParseStmtSymbolDef(module);
			if (symbolDef == null)
				return null;
			module.symbols.Add(symbolDef.name, symbolDef);
		}

		return module;
	}

	AstNodeUsing ParseUsing(AstNode p)
	{
		if (!this.CheckToken(Token.Type.USING))
			return null;

		AstNodeUsing node = new AstNodeUsing(p);

		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;
		
		node.name = this.token.value;
		this.NextToken();

		if (!this.CheckToken(Token.Type.COLON))
			return null;

		var path = this.ParseLiteralString(p) as AstNodeLiteralString;
		if (path == null)
			return null;

		node.path = path.value;
		
		return node;
	}

	/**
	 * type := ID '<' type '>'
	*/
	AstNodeType ParseType(AstNode p)
	{
		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		String name = this.token.value;
		var node = new AstNodeType(p);
		node.name = name;

		this.NextToken(); // ignore ID

		// <
		if (this.CheckToken(Token.Type.LT, false))
		{
			// >
			while (!this.CheckToken(Token.Type.GT, false))
			{
				if (node.args.Count > 0 && !this.CheckToken(Token.Type.COMMA))
					return null;

				var arg = this.ParseType(node);
				if (arg == null)
					return null;

				node.args.Add(arg);
			}

			if (node.args.Count == 0)
			{
				this.Error("type arguments is empty.");
				return null;
			}
		}

		return node;
	}

	AstNodeExpr ParseExpr(AstNode p)
	{
		return this.ParseExprTrinary(p);
	}

	AstNodeExpr ParseExprTrinary(AstNode p)
	{
		AstNodeExpr binary = this.ParseExprBinary(p, 1);
		if (binary == null)
			return null;

		if (this.CheckToken(Token.Type.QUESTION, false))
		{
			var trinary = new AstNodeExprTrinary(p);
			trinary.cond = binary;
			binary.parent = trinary;

			trinary.branch_true = this.ParseExpr(trinary);
			if (trinary.branch_true == null)
				return null;

			if (!this.CheckToken(Token.Type.COLON))
				return null;

			trinary.branch_false = this.ParseExpr(trinary);
			if (trinary.branch_false == null)
				return null;

			return trinary;
		}

		return binary;
	}

	AstNodeExpr ParseExprBinary(AstNode p, int level)
	{
		AstNodeExpr left = null;

		if (level < (int)AstBinaryOper.MAX_LEVEL / 100)
		{
			left = this.ParseExprBinary(p, level + 1);
		}
		else
		{
			left = this.ParseExprUnary(p);
		}

		if (left == null)
		{
			return null;
		}

		for (;;)
		{
			AstBinaryOper oper = this.CheckBinaryOper(level, false);
			if (oper == AstBinaryOper.UNKNOWN)
				break;


			AstNodeExpr right = null;
			if (level < (int)AstBinaryOper.MAX_LEVEL / 100)
			{
				right = this.ParseExprBinary(p, level + 1);
			}
			else
			{
				right = this.ParseExprUnary(p);
			}

			if (right == null)
			{
				return null;
			}

			var binary = new AstNodeExprBinary(p);
			binary.op = oper;
			binary.left = left;
			binary.right = right;

			left = binary;
		}

		return left;
	}

	/*
	expr_unary := expr_value | expr_construct | expr_suffixed
	expr_value := int | float | str | true | false
	*/
	AstNodeExpr ParseExprUnary(AstNode p)
	{
		AstUnaryOper oper = this.CheckUnaryOper(false, true);

		AstNodeExpr right = null;

		Token token = this.token;
		switch (token.type)
		{
			case Token.Type.INT_B:
			case Token.Type.INT_X:
			case Token.Type.INT_D:
				right = this.ParseLiteralInt(p);
				break;
			case Token.Type.FLOAT:
				right = this.ParseLiteralFloat(p);
				break;
			case Token.Type.STRING:
				right = this.ParseLiteralString(p);
				break;
			case Token.Type.TRUE:
			case Token.Type.FALSE:
				right = this.ParseLiteralBool(p);
				break;
			case Token.Type.FUNC:
				right = this.ParseFuncDef(p);
				break;
			case Token.Type.MAKE:
				right = this.ParseObjectDef(p);
				break;
			/*
			case Token.Type.Using:
				right = this.ParseModule_ref(p);
				break;
			*/
			case Token.Type.LSB:
				right = this.ParseArrayDef(p);
				break;
			case Token.Type.ID:
			case Token.Type.LRB:
				right = this.ParseExprSuffixed(p);
				break;
			default:
				this.ErrorTokenUnexpected();
				return null;
		}

		if (oper == AstUnaryOper.UNKNOWN)
			return right;

		var unary = new AstNodeExprUnary(p);
		unary.op = oper;
		unary.right = right;
		right.parent = unary;

		return unary;
	}

	/*
	expr_suffixed := expr_primary{ '.'ID | ':'ID | '['expr']' | '{'stat_list'}' | proc_args }
	*/
	AstNodeExpr ParseExprSuffixed(AstNode p)
	{
		AstNodeExpr primary = this.ParseExprPrimary(p);
		if (primary == null)
			return null;

		for (;;)
		{
			AstNodeExpr suffixed = null;

			Token token = this.token;
			switch (token.type)
			{
				case Token.Type.DOT: // .
					suffixed = this.ParseObjectRef(p, primary);
					break;
				case Token.Type.LSB: // [
					suffixed = this.ParseIndexRef(p, primary);
					break;
				case Token.Type.LRB: // (
					suffixed = this.ParseFuncCall(p, primary);
					break;
				default: // no more invalid suffix
					return primary;
			}

			if (suffixed == null)
				return null;

			primary = suffixed;
		}

		return primary;
	}

	/*
	expr_primary := ID | '(' expr ')'
	*/
	AstNodeExpr ParseExprPrimary(AstNode p)
	{
		if (this.CheckToken(Token.Type.LRB, false))
		{
			AstNodeExpr expr = this.ParseExpr(p);
			if (expr == null)
				return null;

			if (!this.CheckToken(Token.Type.RRB))
				return null;

			return expr;
		}

		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		var node = new AstNodeSymbolRef(p);
		node.name = this.token.value;

		this.NextToken();

		return node;
	}

	AstNodeExpr ParseLiteralInt(AstNode p)
	{
		Token token = this.token;
		switch (token.type)
		{
			case Token.Type.INT_B:
			{
				var node = new AstNodeLiteralInt(p);
				long.TryParse(token.value, out node.value);
				this.NextToken();
				return node;
			}
			case Token.Type.INT_X:
			{
				var node = new AstNodeLiteralInt(p);
				long.TryParse(token.value, out node.value);
				this.NextToken();
				return node;
			}
			case Token.Type.INT_D:
			{
				var node = new AstNodeLiteralInt(p);
				long.TryParse(token.value, out node.value);
				this.NextToken();
				return node;
			}
			default:
				this.ErrorTokenUnexpected();
				return null;
		}
	}

	AstNodeExpr ParseLiteralFloat(AstNode p)
	{
		Token token = this.token;
		switch (token.type)
		{
			case Token.Type.FLOAT:
			{
				var node = new AstNodeLiteralFloat(p);
				double.TryParse(token.value, out node.value);
				this.NextToken();
				return node;
			}
			default:
				this.ErrorTokenUnexpected();
				return null;
		}
	}

	AstNodeExpr ParseLiteralBool(AstNode p)
	{
		Token token = this.token;
		switch (token.type)
		{
			case Token.Type.TRUE:
			case Token.Type.FALSE:
			{
				var node = new AstNodeLiteralBool(p);
				bool.TryParse(token.value, out node.value);
				this.NextToken();
				return node;
			}
			default:
				this.ErrorTokenUnexpected();
				return null;
		}
	}

	AstNodeExpr ParseLiteralString(AstNode p)
	{
		Token token = this.token;
		switch (token.type)
		{
			case Token.Type.STRING:
			{
				var node = new AstNodeLiteralString(p);
				node.value = token.value;
				this.NextToken();
				return node;
			}
			default:
				this.ErrorTokenUnexpected();
				return null;
		}
	}

	/*
	func_def => 'func' func_params func_body
	*/
	AstNodeExpr ParseFuncDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.FUNC))
			return null;

		var node = new AstNodeFuncDef(p);
		if (!this.ParseFuncParams(node))
			return null;

		// : ret-type
		if (!this.CheckToken(Token.Type.COLON))
			return null;

		node.rtype = this.ParseType(node);
		if (node.rtype == null)
			return null;

		if (!this.ParseFuncBody(node))
			return null;

		return node;
	}

	/*
	func_params => '(' [ID] ')'
	*/
	bool ParseFuncParams(AstNodeFuncDef node)
	{
		if (!this.CheckToken(Token.Type.LRB)) // (
			return false;

		do
		{
			if (this.token.type == Token.Type.RRB)
				break;

			if (!this.CheckToken(Token.Type.ID, true, false))
				return false;
			String name = this.token.value;
			if (node.GetArg(name) != null)
			{
				this.ErrorTokenUnexpected();
				return false;
			}

			this.NextToken();

			if (!this.CheckToken(Token.Type.COLON))
				return false;

			AstNodeType type = this.ParseType(node);
			if (type == null)
				return false;

			var arg = node.AddArg(name);
			if (arg == null)
			{
				this.ErrorTokenUnexpected();
				return false;
			}

			arg.name = name;
			arg.type = type;
		} while (this.CheckToken(Token.Type.COMMA, false));

		if (!this.CheckToken(Token.Type.RRB))
			return false;

		return true;
	}

	/*
	func_body => '{' [stat] '}'
	*/
	bool ParseFuncBody(AstNodeFuncDef node)
	{
		if (!this.CheckToken(Token.Type.LCB))
			return false;

		while (!this.CheckToken(Token.Type.RCB, false))
		{
			AstNodeStmt stmt = this.ParseStmt(node);
			if (stmt == null)
				return false;
			node.body.Add(stmt);
		}

		return true;
	}

	/*
	func_call => '(' expr, expr, ..., expr ')'
	*/
	AstNodeExpr ParseFuncCall(AstNode p, AstNodeExpr primary)
	{
		var node = new AstNodeFuncRef(p);

		if (!this.CheckToken(Token.Type.LRB))
			return null;

		while (!this.CheckToken(Token.Type.RRB, false))
		{
			if (node.args.Count > 0 && !this.CheckToken(Token.Type.COMMA))
				return null;

			AstNodeExpr arg = this.ParseExpr(node);
			if (arg == null)
				return null;

			node.args.Add(arg);
		}

		// 确保所有解析成功后，才能将 primary 赋值给 node，
		// 否则会出现 crash。
		node.func = primary;
		primary.parent = node;

		return node;
	}

	/*
	object_def => 'make' type_ref '{' [object_field {sep object_field} [sep]] '}'
	sep => ',' | ';'
	*/
	AstNodeExpr ParseObjectDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.MAKE))
			return null;

		var node = new AstNodeObjectDef(p);
		node.type = this.ParseType(node);
		if (node.type == null)
			return null;

		if (!this.CheckToken(Token.Type.LCB))
			return null;

		do
		{
			if (this.token.type == Token.Type.RCB)
				break;
			if (!this.ParseObjectField(node))
				return null;
		} while (this.CheckToken(Token.Type.COMMA, false));

		if (!this.CheckToken(Token.Type.RCB))
			return null;

		return node;
	}

	/*
	object_field => ID '=' expr
	*/
	bool ParseObjectField(AstNodeObjectDef node)
	{
		if (!this.CheckToken(Token.Type.ID, true, false))
			return false;

		String key = this.token.value;
		this.NextToken();

		if (!this.CheckToken(Token.Type.ASSIGN, false) && !this.CheckToken(Token.Type.COLON, false))
		{
			return false;
		}

		AstNodeExpr expr = this.ParseExpr(node);
		if (expr == null)
			return false;

		node.members[key] = expr;

		return true;
	}

	/*
	array_def => '[' [array_field {sep array_field} [sep]] ']'
	sep => ',' | ';'
	*/
	AstNodeExpr ParseArrayDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.LSB))
			return null;

		var node = new AstNodeArrayDef(p);

		do
		{
			if (this.token.type == Token.Type.RSB)
				break;

			AstNodeExpr expr = this.ParseExpr(node);
			if (expr == null)
				return null;

			node.elements.Add(expr);
		} while (this.CheckToken(Token.Type.COMMA, false));

		if (!this.CheckToken(Token.Type.RSB))
			return null;

		return node;
	}

	/*
	index_ref => '[' expr ']'
	*/
	AstNodeExpr ParseIndexRef(AstNode p, AstNodeExpr primary)
	{
		var node = new AstNodeArrayRef(p);

		if (!this.CheckToken(Token.Type.LSB))
			return null;

		node.key = this.ParseExpr(node);
		if (node.key == null)
			return null;

		if (!this.CheckToken(Token.Type.RSB))
			return null;

		node.obj = primary;
		primary.parent = node;

		return node;
	}

	/*
	object_ref => '.' ID
	*/
	AstNodeExpr ParseObjectRef(AstNode p, AstNodeExpr primary)
	{
		var node = new AstNodeObjectRef(p);

		if (!this.CheckToken(Token.Type.DOT))
			return null;

		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		node.key = this.token.value;

		this.NextToken();

		node.obj = primary;
		primary.parent = node;

		return node;
	}

	AstNodeStmt ParseStmt(AstNode p)
	{
		AstNodeStmt stmt = null;
		bool semicolon = false;

		switch (this.token.type)
		{
			case Token.Type.STRUCT:
				stmt = this.ParseStmtStructDef(p);
				break;
			case Token.Type.ENUM:
				stmt = this.ParseStmtEnumDef(p);
				break;
			case Token.Type.PROC:
				stmt = this.ParseStmtProcDef(p);
				semicolon = true;
				break;
			case Token.Type.VAR:
			case Token.Type.VAL:
				stmt = this.ParseStmtSymbolDef(p);
				semicolon = true;
				break;
			case Token.Type.BREAK:
				stmt = this.ParseStmtBreak(p);
				semicolon = true;
				break;
			case Token.Type.CONTINUE:
				stmt = this.ParseStmtContinue(p);
				semicolon = true;
				break;
			case Token.Type.RETURN:
				stmt = this.ParseStmtReturn(p);
				semicolon = true;
				break;
			case Token.Type.IF:
				stmt = this.ParseStmtIf(p);
				break;
			case Token.Type.LOOP:
				stmt = this.ParseStmtLoop(p);
				break;
			case Token.Type.LCB:
				stmt = this.ParseStmtBlock(p);
				break;
			default:
				stmt = this.ParseStmt_assign_or_call(p);
				semicolon = true;
				break;
		}

		if (semicolon && !this.CheckToken(Token.Type.SEMICOLON))
			return null;

		return stmt;
	}

	/**
	 * struct_def := 'struct' ID '{' struct_member '};';
	*/
	AstNodeStructDef ParseStmtStructDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.STRUCT))
			return null;

		var node = new AstNodeStructDef(p);

		// ID
		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		node.name = this.token.value;
		this.NextToken();

		// {
		if (!this.CheckToken(Token.Type.LCB))
			return null;

		do
		{
			if (this.token.type == Token.Type.RCB)
				break;

			if (!this.ParseStmt_struct_member(node))
				return null;
		} while (this.CheckToken(Token.Type.SEMICOLON, false));

		// }
		if (!this.CheckToken(Token.Type.RCB))
			return null;

		return node;
	}

	/**
	 * struct_member := ('var' | 'val') ID : type [ '=' expr ] ;
	*/
	bool ParseStmt_struct_member(AstNodeStructDef p)
	{
		// (val | var)
		bool isConst = false;
		switch (this.token.type)
		{
			case Token.Type.VAL:
				isConst = true;
				break;
			case Token.Type.VAR:
				isConst = false;
				break;
			default:
				return false;
		}

		this.NextToken();

		// ID
		if (!this.CheckToken(Token.Type.ID, true, false))
			return false;

		String name = this.token.value;
		var node = p.AddMember(name);
		if (node == null)
		{
			this.ErrorTokenUnexpected();
			return false;
		}

		this.NextToken();

		// : type
		if (!this.CheckToken(Token.Type.COLON))
			return false;
		node.type = this.ParseType(p);
		if (node.type == null)
			return false;

		// [= expr]
		if (this.CheckToken(Token.Type.ASSIGN, false))
		{
			node.value = this.ParseExpr(p);
			if (node.value == null)
				return false;
		}

		return true;
	}

	/**
	 * enum_def := 'enum' ID '{' [enum_member] '}' ';';
	 * enum_member := ID ['=' expr_int] ',';
	 * */
	AstNodeEnumDef ParseStmtEnumDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.ENUM))
			return null;

		var node = new AstNodeEnumDef(p);

		// ID
		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		node.name = this.token.value;
		this.NextToken();

		// {
		if (!this.CheckToken(Token.Type.LCB))
			return null;

		int index = 0;
		do
		{
			if (this.token.type == Token.Type.RCB)
				break;

			// ID
			if (!this.CheckToken(Token.Type.ID, true, false))
				return null;

			String memName = this.token.value;
			if (node.members.ContainsKey(memName))
			{
				this.ErrorTokenUnexpected();
				return null;
			}

			this.NextToken();

			// [= int]
			int memValue = index;
			if (this.CheckToken(Token.Type.ASSIGN, false))
			{
				var memExpr = this.ParseLiteralInt(node);
				if (memExpr == null)
					return null;
				var memIntExpr = memExpr as AstNodeLiteralInt;
				memValue = (int)(memIntExpr.value);
				index = memValue;
			}

			node.members[memName] = memValue;
			index += 1;
		} while (this.CheckToken(Token.Type.COMMA, false));

		// }
		if (!this.CheckToken(Token.Type.RCB))
			return null;

		return node;
	}

	/**
	 * proc_def := 'proc' ID '(' [func_params]* ')' ';';
	 * func_params := ID ':' type_ref ','
	*/
	AstNodeProcDef ParseStmtProcDef(AstNode p)
	{
		if (!this.CheckToken(Token.Type.PROC))
			return null;

		var node = new AstNodeProcDef(p);

		// ID
		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		node.name = this.token.value;
		this.NextToken();

		// (
		if (!this.CheckToken(Token.Type.LRB))
			return null;

		do
		{
			if (this.token.type == Token.Type.RRB)
				break;

			// ID
			if (!this.CheckToken(Token.Type.ID, true, false))
				return null;

			String argName = this.token.value;
			if (node.args.ContainsKey(argName))
			{
				this.ErrorTokenUnexpected();
				return null;
			}

			this.NextToken();

			// : type
			if (!this.CheckToken(Token.Type.COLON))
				return null;
			AstNodeType argType = this.ParseType(node);
			if (argType == null)
				return null;

			node.args[argName] = argType;
		} while (this.CheckToken(Token.Type.COMMA, false));

		// )
		if (!this.CheckToken(Token.Type.RRB))
			return null;

		// : ret-type
		if (!this.CheckToken(Token.Type.COLON))
			return null;
		node.type = this.ParseType(node);
		if (node.type == null)
			return null;

		return node;
	}

	/**
	 * symbol_def := 'var' | 'val' ID [: type] = expr;
	*/
	AstNodeSymbolDef ParseStmtSymbolDef(AstNode p)
	{
		bool isPublic = false;
		if (p != null && (p.category == AstCategory.MODULE || p.category == AstCategory.STRUCT_DEF))
		{
			isPublic = this.CheckToken(Token.Type.PUBLIC, false);
		}
		
		bool isVal = this.CheckToken(Token.Type.VAL, false);
		bool isVar = this.CheckToken(Token.Type.VAR, false);
		if (isVal == isVar)
		{
			this.ErrorTokenUnexpected();
			return null;
		}

		var node = new AstNodeSymbolDef(p);
		node.isPublic = isPublic;
		node.isVariable = isVar;

		this.NextToken(); // ignore 'var' | 'val'

		if (!this.CheckToken(Token.Type.ID, true, false))
			return null;

		node.name = this.token.value;

		this.NextToken();

		// : type
		if (this.CheckToken(Token.Type.COLON, false))
		{
			node.type = this.ParseType(p);
			if (node.type == null)
				return null;
		}

		// = expr
		if (!this.CheckToken(Token.Type.ASSIGN, true))
			return null;

		node.value = this.ParseExpr(node);
		if (node.value == null)
			return null;

		return node;
	}

	AstNodeContinue ParseStmtContinue(AstNode p)
	{
		if (!this.CheckToken(Token.Type.CONTINUE))
			return null;

		var node = new AstNodeContinue(p);

		return node;
	}

	AstNodeBreak ParseStmtBreak(AstNode p)
	{
		if (!this.CheckToken(Token.Type.BREAK))
			return null;

		var node = new AstNodeBreak(p);

		return node;
	}

	AstNodeReturn ParseStmtReturn(AstNode p)
	{
		if (!this.CheckToken(Token.Type.RETURN))
			return null;

		var node = new AstNodeReturn(p);

		if (this.CheckToken(Token.Type.SEMICOLON, false, false))
			return node;

		node.value = this.ParseExpr(node);
		if (node.value == null)
			return null;

		return node;
	}

	AstNodeIf ParseStmtIf(AstNode p)
	{
		if (!this.CheckToken(Token.Type.IF))
			return null;

		var node = new AstNodeIf(p);

		if (!this.CheckToken(Token.Type.LRB))
			return null;

		node.cond = this.ParseExpr(node);
		if (node.cond == null)
			return null;

		if (!this.CheckToken(Token.Type.RRB))
			return null;

		node.branch_true = this.ParseStmt(node);
		if (node.branch_true == null)
			return null;

		if (this.CheckToken(Token.Type.ELSE, false))
		{
			node.branch_false = this.ParseStmt(node);
			if (node.branch_false == null)
				return null;
		}

		return node;
	}

	AstNodeLoop ParseStmtLoop(AstNode p)
	{
		if (!this.CheckToken(Token.Type.LOOP))
			return null;

		var node = new AstNodeLoop(p);

		if (!this.CheckToken(Token.Type.LRB))
			return null;

		node.init = this.ParseStmtLoop_init(node);
		if (node.init == null)
			return null;

		node.cond = this.ParseStmtLoop_cond(node);
		if (node.cond == null)
			return null;

		node.step = this.ParseStmtLoop_step(node);
		if (node.step == null)
			return null;

		if (!this.CheckToken(Token.Type.RRB))
			return null;

		node.body = this.ParseStmt(node);
		if (node.body == null)
			return null;

		return node;
	}

	AstNodeStmt ParseStmtLoop_init(AstNode p)
	{
		var stmt = this.ParseStmtSymbolDef(p);
		if (stmt == null)
			return null;

		if (!this.CheckToken(Token.Type.SEMICOLON))
			return null;

		return stmt;
	}

	AstNodeExpr ParseStmtLoop_cond(AstNode p)
	{
		AstNodeExpr cond = this.ParseExpr(p);
		if (cond == null)
			return null;

		if (!this.CheckToken(Token.Type.SEMICOLON))
			return null;

		return cond;
	}

	AstNodeStmt ParseStmtLoop_step(AstNode p)
	{
		AstNodeStmt stmt = null;
		switch (this.token.type)
		{
			case Token.Type.VAR:
			case Token.Type.VAL:
				stmt = this.ParseStmtSymbolDef(p);
				break;
			default:
				stmt = this.ParseStmt_assign_or_call(p);
				break;
		}

		return stmt;
	}

	AstNodeBlock ParseStmtBlock(AstNode p)
	{
		if (!this.CheckToken(Token.Type.LCB))
			return null;

		var node = new AstNodeBlock(p);

		while (!this.CheckToken(Token.Type.RCB, false))
		{
			var stmt = this.ParseStmt(node);
			if (stmt == null)
				return null;

			node.stmts.Add(stmt);

			this.CheckToken(Token.Type.SEMICOLON, false);
		}

		return node;
	}

	AstNodeStmt ParseStmt_assign_or_call(AstNode p)
	{
		AstNodeExpr left = this.ParseExprSuffixed(p);
		if (left == null)
			return null;

		if (this.CheckToken(Token.Type.ASSIGN, false))
		{
			var node = new AstNodeAssign(p);
			node.left = left;
			left.parent = node;

			node.right = this.ParseExpr(node);
			if (node.right == null)
				return null;

			return node;
		}
		else if (left.category == AstCategory.FUNC_REF)
		{
			var node = new AstNodeInvoke(p);
			node.expr = left as AstNodeFuncRef;
			left.parent = node;

			return node;
		}
		else
		{
			this.ErrorTokenUnexpected();
			return null;
		}
	}

	void NextToken()
	{
		this.lexer.NextToken();
	}

	Token token => this.lexer.token;

	Token look_ahead_token()
	{
		return this.lexer.lookAheadToken;
	}

	bool CheckToken(Token.Type tokenType, bool required = true, bool movenext = true)
	{
		if (this.lexer.token.type != tokenType)
		{
			if (required)
			{
				this.ErrorTokenUnexpected();
			}

			return false;
		}

		if (movenext)
		{
			this.lexer.NextToken();
		}

		return true;
	}

	AstUnaryOper CheckUnaryOper(bool required, bool movenext = true)
	{
		AstUnaryOper oper = AstUnaryOper.UNKNOWN;
		switch (this.lexer.token.type)
		{
			case Token.Type.ADD:
				oper = AstUnaryOper.POS;
				break;
			case Token.Type.SUB:
				oper = AstUnaryOper.NEG;
				break;
			case Token.Type.NOT:
				oper = AstUnaryOper.NOT;
				break;
			case Token.Type.FLIP:
				oper = AstUnaryOper.FLIP;
				break;
			case Token.Type.AT:
				oper = AstUnaryOper.TYPE_OF;
				break;
			case Token.Type.POUND:
				oper = AstUnaryOper.SIZE_OF;
				break;
			default:
				break;
		}

		if (oper == AstUnaryOper.UNKNOWN)
		{
			if (required)
			{
				this.ErrorTokenUnexpected();
			}

			return oper;
		}

		if (movenext)
		{
			this.lexer.NextToken();
		}

		return oper;
	}

	AstBinaryOper CheckBinaryOper(int level, bool required, bool movenext = true)
	{
		AstBinaryOper oper = AstBinaryOper.UNKNOWN;
		switch (this.lexer.token.type)
		{
			case Token.Type.OR2:
				oper = AstBinaryOper.OR;
				break;
			case Token.Type.AND2:
				oper = AstBinaryOper.AND;
				break;
			case Token.Type.EQ:
				oper = AstBinaryOper.EQ;
				break;
			case Token.Type.GT:
				oper = AstBinaryOper.GT;
				break;
			case Token.Type.LT:
				oper = AstBinaryOper.LT;
				break;
			case Token.Type.GE:
				oper = AstBinaryOper.GE;
				break;
			case Token.Type.LE:
				oper = AstBinaryOper.LE;
				break;
			case Token.Type.NE:
				oper = AstBinaryOper.NE;
				break;
			case Token.Type.ADD:
				oper = AstBinaryOper.ADD;
				break;
			case Token.Type.SUB:
				oper = AstBinaryOper.SUB;
				break;
			case Token.Type.MUL:
				oper = AstBinaryOper.MUL;
				break;
			case Token.Type.DIV:
				oper = AstBinaryOper.DIV;
				break;
			case Token.Type.MOD:
				oper = AstBinaryOper.MOD;
				break;
			case Token.Type.AND:
				oper = AstBinaryOper.BIT_AND;
				break;
			case Token.Type.OR:
				oper = AstBinaryOper.BIT_OR;
				break;
			case Token.Type.XOR:
				oper = AstBinaryOper.BIT_XOR;
				break;
			case Token.Type.SHIFT_L:
				oper = AstBinaryOper.SHIFT_L;
				break;
			case Token.Type.SHIFT_R:
				oper = AstBinaryOper.SHIFT_R;
				break;
			default:
				break;
		}

		if ((int)oper / 100 != level)
		{
			oper = AstBinaryOper.UNKNOWN;
		}

		if (oper == AstBinaryOper.UNKNOWN)
		{
			if (required)
			{
				this.ErrorTokenUnexpected();
			}

			return oper;
		}

		if (movenext)
		{
			this.lexer.NextToken();
		}

		return oper;
	}

	void Error(string fmt, params object?[] args)
	{
		string message = string.Format(fmt, args);
		this.error = string.Format("{0} at {1}, {2}.\n", message, this.lexer.line, this.lexer.column);
	}

	void ErrorTokenUnexpected()
	{
		Token token = this.lexer.token;
		string value = token.value;
		if (token.type == Token.Type.EOS)
		{
			this.Error("unexpected eos");
		}
		else
		{
			this.Error("unexpected token '{0}'", value);
		}
	}
}
