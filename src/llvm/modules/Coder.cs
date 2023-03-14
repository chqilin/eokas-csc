
using LLVMSharp;

namespace Eokas.Modules;

public class Coder : Module
{
	private Scope scope;
	private LLVMBuilderRef builder;

	private LLVMBasicBlockRef continuePoint;
	private LLVMBasicBlockRef breakingPoint;

	public string error { get; private set; }

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
			this.scope.AddSchema("i8", Model.TyI8);
			this.scope.AddSchema("i16", Model.TyI16);
			this.scope.AddSchema("i32", Model.TyI32);
			this.scope.AddSchema("i64", Model.TyI64);
			this.scope.AddSchema("u8", Model.TyI8);
			this.scope.AddSchema("u16", Model.TyI16);
			this.scope.AddSchema("u32", Model.TyI32);
			this.scope.AddSchema("u64", Model.TyI64);
			this.scope.AddSchema("bool", Model.TyBool);
			
			var entry = LLVM.AppendBasicBlock(this.scope.func, "entry");
			LLVM.PositionBuilderAtEnd(this.builder, entry);

			// Types
			foreach (var item in node.types)
			{
				var name = item.Key;
				var schema = item.Value;
				var type = this.EncodeType(schema);

				if (!this.scope.AddSchema(name, type))
				{
					this.ErrorSymbolIsAlreadyDefined(name);
					return false;
				}
			}

			// Values or Variables
			foreach (var item in node.symbols)
			{
				var name = item.Key;
				var symbol = item.Value;

				var expr = this.EncodeExpr(symbol.value);
				if (expr.Pointer == IntPtr.Zero)
					return false;
				
				if (!this.scope.AddSymbol(name, expr))
				{
					this.ErrorSymbolIsAlreadyDefined(name);
					return false;
				}

				if (symbol.isPublic)
				{
					var type = LLVM.TypeOf(expr);
					var ptr = LLVM.AddGlobal(this.handle, type, name);
					LLVM.BuildStore(this.builder, expr, ptr);
				}
			}

			// LastOp is Terminator?
			var lastBlock = LLVM.GetInsertBlock(this.builder);
			if(!Model.IsLastInstATerminator(lastBlock))
			{
				var retT = this.entry.TypeOf().GetReturnType();
				if (retT.TypeKind == LLVMTypeKind.LLVMVoidTypeKind)
					LLVM.BuildRetVoid(this.builder);
				else
					LLVM.BuildRet(this.builder, Model.GetDefaultValue(retT));
			}
		}
		this.PopScope();
		return true;
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
		if (node == null)
			return default;
		
		// TODO: implements array by Generic-Type
		if (node.name == "array")
		{
			var elementT = this.EncodeType(node.args[0]);
			if (elementT.Pointer == IntPtr.Zero)
			{
				this.Error("Invalid array element type");
				return default;
			}

			// TODO: array type
			// return module.define_schema_array(elementT);
			return default;
		}

		var schema = this.scope.GetSchema(node.name, true);
		if (schema == null)
		{
			this.Error("The type '{0}' is undefined.", node.name);
			return default;
		}

		if (schema.generics.Count != node.args.Count)
		{
			this.Error("The generic type defination is not matched with the arguments.");
			return default;
		}

		if (schema.generics.Count > 0)
		{
			List<LLVMTypeRef> typeArgs = new List<LLVMTypeRef>();
			foreach (var arg in node.args)
			{
				var ty = this.EncodeType(arg);
				if (ty.Pointer == IntPtr.Zero)
				{
					this.Error("Type Args is invalid.");
					return default;
				}

				typeArgs.Add(ty);
			}

			return schema.ResolveGenerics(typeArgs);
		}

		return schema.type;
	}

	private LLVMValueRef EncodeExpr(AstNodeExpr node)
	{
		if (node == null)
			return default;
		
		switch (node.category)
		{
			case AstCategory.EXPR_TRINARY:
				return this.EncodeExprTrinary(node as AstNodeExprTrinary);
			case AstCategory.EXPR_BINARY:
				return this.EncodeExprBinary(node as AstNodeExprBinary);
			case AstCategory.EXPR_UNARY:
				return this.EncodeExprUnary(node as AstNodeExprUnary);
			case AstCategory.LITERAL_INT:
				return this.EncodeExprInt(node as AstNodeLiteralInt);
			case AstCategory.LITERAL_FLOAT:
				return this.EncodeExprFloat(node as AstNodeLiteralFloat);
			case AstCategory.LITERAL_BOOL:
				return this.EncodeExprBool(node as AstNodeLiteralBool);
			case AstCategory.LITERAL_STRING:
				return this.EncodeExprString(node as AstNodeLiteralString);
			case AstCategory.SYMBOL_REF:
				return this.EncodeExprSymbolRef(node as AstNodeSymbolRef);
			case AstCategory.FUNC_DEF:
				return this.EncodeExprFuncDef(node as AstNodeFuncDef);
			case AstCategory.FUNC_REF:
				return this.EncodeExprFuncRef(node as AstNodeFuncRef);
			case AstCategory.ARRAY_DEF:
				return this.EncodeExprArrayDef(node as AstNodeArrayDef);
			case AstCategory.ARRAY_REF:
				return this.EncodeExprIndexRef(node as AstNodeArrayRef);
			case AstCategory.OBJECT_DEF:
				return this.EncodeExprObjectDef(node as AstNodeObjectDef);
			case AstCategory.OBJECT_REF:
				return this.EncodeExprObjectRef(node as AstNodeObjectRef);
			default:
				return default;
		}

		return default;
	}

	LLVMValueRef EncodeExprTrinary(AstNodeExprTrinary node)
	{
		if (node == null)
			return default;
		
		var trinaryBegin = LLVM.AppendBasicBlock(this.scope.func, "trinary.begin");
		var trinaryTrue = LLVM.AppendBasicBlock(this.scope.func, "trinary.true");
		var trinaryFalse = LLVM.AppendBasicBlock(this.scope.func, "trinary.false");
		var trinaryEnd = LLVM.AppendBasicBlock(this.scope.func, "trinary.end");

		LLVM.BuildBr(this.builder, trinaryBegin);
		LLVM.PositionBuilderAtEnd(this.builder, trinaryBegin);

		// Condition
		var cond = this.EncodeExpr(node.cond);
		if (cond.Pointer == IntPtr.Zero)
			return default;
		cond = Model.GetValue(builder, cond);
		if (!cond.TypeOf().IsIntegerTy(1))
		{
			this.Error("Condition is not a bool value.");
			return default;
		}

		LLVM.BuildCondBr(this.builder, cond, trinaryTrue, trinaryFalse);

		// True
		LLVM.PositionBuilderAtEnd(this.builder, trinaryTrue);
		var trueV = this.EncodeExpr(node.branch_true);
		if (trueV.Pointer == IntPtr.Zero)
			return default;
		LLVM.BuildBr(this.builder, trinaryEnd);

		// False
		LLVM.PositionBuilderAtEnd(this.builder, trinaryFalse);
		var falseV = this.EncodeExpr(node.branch_false);
		if (falseV.Pointer == IntPtr.Zero)
			return default;
		LLVM.BuildBr(this.builder, trinaryEnd);

		// PHI
		LLVM.PositionBuilderAtEnd(this.builder, trinaryEnd);
		if (trueV.TypeOf().TypeKind != falseV.TypeOf().TypeKind)
		{
			this.Error("Type of true-branch and false-branch is not same.");
			return default;
		}

		var phi = LLVM.BuildPhi(this.builder, trueV.TypeOf(), "");
		phi.AddIncoming(new[] { trueV, falseV }, new[] { trinaryTrue, trinaryFalse }, 2);

		return phi;
	}

	LLVMValueRef EncodeExprBinary(AstNodeExprBinary node)
	{
		if (node == null)
			return default;
		
		var left = this.EncodeExpr(node.left);
		var right = this.EncodeExpr(node.right);
		if (left.Pointer == IntPtr.Zero || right.Pointer == IntPtr.Zero)
			return default;

		var lhs = Model.GetValue(builder, left);
		var rhs = Model.GetValue(builder, right);

		switch (node.op)
		{
			case AstBinaryOper.OR:
				return this.EncodeExprBinaryOR(lhs, rhs);
			case AstBinaryOper.AND:
				return this.EncodeExprBinaryAND(lhs, rhs);
			case AstBinaryOper.EQ:
				return this.EncodeExprBinaryEQ(lhs, rhs);
			case AstBinaryOper.NE:
				return this.EncodeExprBinaryNE(lhs, rhs);
			case AstBinaryOper.LE:
				return this.EncodeExprBinaryLE(lhs, rhs);
			case AstBinaryOper.GE:
				return this.EncodeExprBinaryGE(lhs, rhs);
			case AstBinaryOper.LT:
				return this.EncodeExprBinaryLT(lhs, rhs);
			case AstBinaryOper.GT:
				return this.EncodeExprBinaryGT(lhs, rhs);
			case AstBinaryOper.ADD:
				return this.EncodeExprBinaryADD(lhs, rhs);
			case AstBinaryOper.SUB:
				return this.EncodeExprBinarySUB(lhs, rhs);
			case AstBinaryOper.MUL:
				return this.EncodeExprBinaryMUL(lhs, rhs);
			case AstBinaryOper.DIV:
				return this.EncodeExprBinaryDIV(lhs, rhs);
			case AstBinaryOper.MOD:
				return this.EncodeExprBinaryMOD(lhs, rhs);
			case AstBinaryOper.BIT_AND:
				return this.EncodeExprBinaryBITAND(lhs, rhs);
			case AstBinaryOper.BIT_OR:
				return this.EncodeExprBinaryBITOR(lhs, rhs);
			case AstBinaryOper.BIT_XOR:
				return this.EncodeExprBinaryBITXOR(lhs, rhs);
			case AstBinaryOper.SHIFT_L:
				return this.EncodeExprBinarySHL(lhs, rhs);
			case AstBinaryOper.SHIFT_R:
				return this.EncodeExprBinarySHR(lhs, rhs);
			default:
				return default;
		}
	}

	LLVMValueRef EncodeExprBinaryOR(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if ((!ltype.IsIntegerTy(1)) || (!rtype.IsIntegerTy(1)))
		{
			this.Error("LHS or RHS is not bool value. \n");
			return default;
		}

		return LLVM.BuildOr(this.builder, lhs, rhs, "");
	}

	LLVMValueRef EncodeExprBinaryAND(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if ((!ltype.IsIntegerTy(1)) || (!rtype.IsIntegerTy(1)))
		{
			this.Error("LHS or RHS is not bool value. \n");
			return default;
		}

		return LLVM.BuildAnd(this.builder, lhs, rhs, "");
	}

	LLVMValueRef EncodeExprBinaryEQ(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntEQ, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOEQ, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntEQ, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOEQ, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOEQ, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.");
		return default;
	}

	LLVMValueRef EncodeExprBinaryNE(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntNE, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealONE, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntNE, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealONE, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealONE, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryLE(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntSLE, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLE, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntULE, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLE, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLE, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryGE(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntSGE, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGE, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntUGE, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGE, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGE, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryLT(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntSLT, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLT, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntULT, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLT, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOLT, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryGT(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntSGT, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGT, lhs, rhs, "");

		if (ltype.IsPointerTy() && rtype.IsPointerTy())
		{
			var l = LLVM.BuildPtrToInt(this.builder, lhs, Model.TyI64, "");
			var r = LLVM.BuildPtrToInt(this.builder, rhs, Model.TyI64, "");
			return LLVM.BuildICmp(this.builder, LLVMIntPredicate.LLVMIntUGT, l, r, "");
		}

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGT, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFCmp(this.builder, LLVMRealPredicate.LLVMRealOGT, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryADD(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildAdd(this.builder, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFAdd(this.builder, lhs, rhs, "");

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFAdd(this.builder, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFAdd(this.builder, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinarySUB(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildSub(this.builder, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFSub(this.builder, lhs, rhs, "");

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFSub(this.builder, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFSub(this.builder, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryMUL(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildMul(this.builder, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFMul(this.builder, lhs, rhs, "");

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFMul(this.builder, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFMul(this.builder, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryDIV(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildSDiv(this.builder, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFDiv(this.builder, lhs, rhs, "");

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFDiv(this.builder, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFDiv(this.builder, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryMOD(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
			return LLVM.BuildSRem(this.builder, lhs, rhs, "");

		if (ltype.IsFloatingPointTy() && rtype.IsFloatingPointTy())
			return LLVM.BuildFRem(this.builder, lhs, rhs, "");

		if (ltype.IsIntegerTy() && rtype.IsFloatingPointTy())
		{
			var l = LLVM.BuildSIToFP(this.builder, lhs, Model.TyF64, "");
			return LLVM.BuildFRem(this.builder, l, rhs, "");
		}

		if (ltype.IsFloatingPointTy() && rtype.IsIntegerTy())
		{
			var r = LLVM.BuildSIToFP(this.builder, rhs, Model.TyF64, "");
			return LLVM.BuildFRem(this.builder, lhs, r, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryBITAND(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
		{
			return LLVM.BuildAnd(this.builder, lhs, rhs, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryBITOR(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
		{
			return LLVM.BuildOr(this.builder, lhs, rhs, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinaryBITXOR(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
		{
			return LLVM.BuildXor(this.builder, lhs, rhs, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinarySHL(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
		{
			return LLVM.BuildShl(this.builder, lhs, rhs, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprBinarySHR(LLVMValueRef lhs, LLVMValueRef rhs)
	{
		var ltype = lhs.TypeOf();
		var rtype = rhs.TypeOf();

		if (ltype.IsIntegerTy() && rtype.IsIntegerTy())
		{
			// 逻辑右移：在左边补 0
			// 算术右移：在左边补 符号位
			// 我们采用逻辑右移
			return LLVM.BuildLShr(this.builder, lhs, rhs, "");
		}

		this.Error("Type of LHS or RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprUnary(AstNodeExprUnary node)
	{
		if (node == null)
			return default;
		
		var right = this.EncodeExpr(node.right);
		if (right.Pointer == IntPtr.Zero)
			return default;

		var rhs = Model.GetValue(builder, right);

		switch (node.op)
		{
			case AstUnaryOper.POS:
				return rhs;
			case AstUnaryOper.NEG:
				return this.EncodeExprUnaryNEG(rhs);
			case AstUnaryOper.NOT:
				return this.EncodeExprUnaryNOT(rhs);
			case AstUnaryOper.FLIP:
				return this.EncodeExprUnaryFLIP(rhs);
			default:
				return default;
		}
	}

	LLVMValueRef EncodeExprUnaryNEG(LLVMValueRef rhs)
	{
		var rtype = rhs.TypeOf();

		if (rtype.IsIntegerTy())
			return LLVM.BuildNeg(this.builder, rhs, "");

		if (rtype.IsFloatingPointTy())
			return LLVM.BuildFNeg(this.builder, rhs, "");

		this.Error("Type of RHS is invalid.");
		return default;
	}

	LLVMValueRef EncodeExprUnaryNOT(LLVMValueRef rhs)
	{
		var rtype = rhs.TypeOf();

		if (rtype.IsIntegerTy(1))
			return LLVM.BuildNot(this.builder, rhs, "");

		this.Error("Type of RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprUnaryFLIP(LLVMValueRef rhs)
	{
		var rtype = rhs.TypeOf();

		if (rtype.IsIntegerTy())
		{
			var k = LLVM.ConstInt(Model.TyI32, 0xFFFFFFFF, false);
			return LLVM.BuildXor(this.builder, rhs, k, "");
		}

		this.Error("Type of RHS is invalid.\n");
		return default;
	}

	LLVMValueRef EncodeExprInt(AstNodeLiteralInt node)
	{
		if (node == null)
			return default;
		
		ulong value = (ulong)node.value;
		var type = Model.TyI8;
		if (value > 0xFF)
			type = Model.TyI16;
		else if (value > 0xFFFF)
			type = Model.TyI32;
		else if (value > 0xFFFFFFFF)
			type = Model.TyI64;
		
		return LLVM.ConstInt(type, value, true);
	}

	LLVMValueRef EncodeExprFloat(AstNodeLiteralFloat node)
	{
		if (node == null)
			return default;
		return LLVM.ConstReal(Model.TyF64, node.value);
	}

	LLVMValueRef EncodeExprBool(AstNodeLiteralBool node)
	{
		if (node == null)
			return default;
		return LLVM.ConstInt(Model.TyBool, node.value ? 1UL : 0UL, false);
	}

	LLVMValueRef EncodeExprString(AstNodeLiteralString node)
	{
		if (node == null)
			return default;
		
		/* TODO: 
		var str = module.string_make(this.scope.func, builder, node.value.cstr());
		return str;
		*/
		return LLVM.ConstString(node.value, (uint)node.value.Length, true);
	}

	LLVMValueRef EncodeExprSymbolRef(AstNodeSymbolRef node)
	{
		if (node == null)
			return default;
		
		var symbol = this.scope.GetSymbol(node.name, true);
		if (symbol == null)
		{
			this.ErrorSymbolIsUndefined( node.name);
			return default;
		}

		// local-value-ref
		if (symbol.scope.func.Pointer == this.scope.func.Pointer)
		{
			return symbol.value;
		}

		/*
		// up-value-ref
		{
			var upvalStruct = this.upvals[this.func];
			int index = upvalStruct.get_member_index(node.name);
			if(index < 0)
			{
				upvalStruct.add_member(node.name, symbol.type, symbol.value);
				upvalStruct.resolve();
				index = upvalStruct.get_member_index(node.name);
			}
			if(index < 0)
			{
				this.Error("Create up-value '%s' failed.\n", node.name.cstr());
				return nullptr;
			}
			
			var arg0 = this.func.getArg(0);
			var ptr = builder.CreateConstGEP2_32(arg0.TypeOf().getPointerElementType(), arg0, 0, index);
			return ptr;
		}
		*/

		return symbol.value;
	}

	LLVMValueRef EncodeExprFuncDef(AstNodeFuncDef node)
	{
		if (node == null)
			return default;
		
		var retType = this.EncodeType(node.rtype);
		if (retType.Pointer == IntPtr.Zero)
			return default;

		var argTypes = new List<LLVMTypeRef>();
		foreach (var arg in node.args)
		{
			var argType = this.EncodeType(arg.type);
			if (argType.Pointer == IntPtr.Zero)
				return default;
			if (argType.IsFunctionTy() || argType.IsStructTy() || argType.IsArrayTy())
				argTypes.Add(argType.GetPointerTo());
			else
				argTypes.Add(argType);
		}
		
		var funcType = LLVM.FunctionType(retType, argTypes.ToArray(), false);
		var funcPtr = LLVM.AddFunction(this.handle, "", funcType);
		
		var oldFunc = this.scope.func;
		var oldIB = LLVM.GetInsertBlock(this.builder);
		
		this.PushScope(funcPtr);
		{
			var entry = LLVM.AppendBasicBlock(funcPtr, "entry");
			LLVM.PositionBuilderAtEnd(this.builder, entry);

			// self
			var self = funcPtr;
			this.scope.AddSymbol("self", self);

			// args
			for (uint index = 0; index < node.args.Count; index++)
			{
				var argNode = node.args[(int)index];
				var argVal = LLVM.GetParam(funcPtr, index);

				argVal.SetValueName(argNode.name);

				if (!this.scope.AddSymbol(name, argVal))
				{
					this.ErrorSymbolIsAlreadyDefined(name);
					return default;
				}
			}

			// body
			foreach (var stmt in node.body)
			{
				if (!this.EncodeStmt(stmt))
					return default;
			}

			var lastBlock = LLVM.GetInsertBlock(this.builder);
			if(!Model.IsLastInstATerminator(lastBlock))
			{
				if(retType.IsVoidTy())
					LLVM.BuildRetVoid(this.builder);
				else
					LLVM.BuildRet(this.builder, Model.GetDefaultValue(retType));
			}
		}
		this.PopScope();
		
		LLVM.PositionBuilderAtEnd(this.builder, oldIB);

		return funcPtr;
	}

	LLVMValueRef EncodeExprFuncRef(AstNodeFuncRef node)
	{
		if (node == null)
			return default;
		
		LLVMValueRef funcExpr = this.EncodeExpr(node.func);
		if (funcExpr.Pointer == IntPtr.Zero)
		{
			this.Error("The function is undefined.");
			return default;
		}

		var funcPtr = Model.GetValue(builder, funcExpr);
		var funcType = funcPtr.TypeOf();
		if (funcType.IsFunctionTy())
		{
			// do nothing.
		}
		else if (funcType.IsPointerTy() && funcType.GetPointerElementType().IsFunctionTy())
		{
			funcType = funcType.GetPointerElementType();
		}
		else
		{
			this.Error("Invalid function type.\n");
			return default;
		}

		var paramTypes = funcType.GetParamTypes();
		List<LLVMValueRef> args = new List<LLVMValueRef>();
		for (var i = 0; i < node.args.Count; i++)
		{
			var paramT = paramTypes[i];
			var argV = this.EncodeExpr(node.args[i]);
			if (argV.Pointer == IntPtr.Zero)
				return default;

			argV = Model.GetValue(builder, argV);
			
			if (paramT.Pointer != argV.TypeOf().Pointer)
			{
				/* TODO:
				if (paramT.Pointer == Model.TyBytePtr.Pointer)
				{
					argV = module.cstr_from_value(scope.func, builder, argV);
				}

				if (paramT == module.type_string_ptr)
				{
					argV = module.string_from_value(scope.func, builder, argV);
				}
				else if (!argV.TypeOf().canLosslesslyBitCastTo(paramT))
				{
					this.Error("The type of param[%d] can't cast to the param type of function.\n", i);
					return nullptr;
				}
				*/
			}

			args.Add(argV);
		}
		
		var retVal = LLVM.BuildCall(this.builder, funcPtr, args.ToArray(), "");
		return retVal;
	}

	LLVMValueRef EncodeExprArrayDef(AstNodeArrayDef node)
	{
		if (node == null)
			return default;
		
		LLVMTypeRef arrayElementType = default;
		List<LLVMValueRef> arrayElements = new List<LLVMValueRef>();
		foreach (var element in node.elements)
		{
			var elementV = this.EncodeExpr(element);
			if (elementV.Pointer == IntPtr.Zero)
				return default;
			elementV = Model.GetValue(builder, elementV);

			var elementT = elementV.TypeOf();
			if (arrayElementType.Pointer == IntPtr.Zero)
			{
				arrayElementType = elementT;
			}

			else if (arrayElementType.Pointer != elementT.Pointer)
			{
				this.Error("The type of some elements is not same as others.\n");
				return default;
			}

			arrayElements.Add(elementV);
		}

		if (arrayElements.Count == 0)
		{
			this.Error("The elements of the array is empty.");
			return default;
		}
		
		/* TODO:
		var arrayT = module.define_schema_array(arrayElementType);
		var arrayP = module.make(func, builder, arrayT);
		module.array_set(scope.func, builder, arrayP, arrayElements);

		return arrayP;
		*/
		return default;
	}

	LLVMValueRef EncodeExprIndexRef(AstNodeArrayRef node)
	{
		if (node == null)
			return default;
		
		var objV = this.EncodeExpr(node.obj);
		var keyV = this.EncodeExpr(node.key);
		if (objV.Pointer == IntPtr.Zero || keyV.Pointer == IntPtr.Zero)
			return default;

		objV = Model.GetValue(builder, objV);
		keyV = Model.GetValue(builder, keyV);
		var objT = objV.TypeOf();
		var keyT = keyV.TypeOf();

		/* TODO:
		if (!module.is_schema_array(objT))
		{
			if (keyT.IsIntegerTy())
			{
				return module.array_get(scope.func, builder, objV, keyV);
			}
			else
			{
				this.Error("The type of index is invalid.\n");
				return nullptr;
			}
		}
		else if (objT == module.type_string_ptr)
		{
			if (keyT.IsIntegerTy())
			{
				return module.string_get_char(scope.func, builder, objV, keyV);
			}
			else
			{
				this.Error("The type of index is invalid.\n");
				return default;
			}
		}
		else
		{
			this.Error("Index-Access is not defined on the object.\n");
			return default;
		}*/
		return default;
	}

	LLVMValueRef EncodeExprObjectDef(AstNodeObjectDef node)
	{
		if (node == null)
			return default;
		
		var structType = this.EncodeType(node.type);
		if (structType.Pointer == IntPtr.Zero)
			return default;

		/*
		var structInfo = module.get_struct(structType);
		if (structInfo == nullptr)
			return nullptr;

		for (var objectMember: node.members)
		{
			var structMember = structInfo.get_member(objectMember.first);
			if (structMember == nullptr)
			{
				this.Error("Object member '%s' is not defined in struct.\n", objectMember.first.cstr());
				return nullptr;
			}
			// todo: check object-member-type equals to struct-member-type.
		}

		// make object instance
		LLVMValueRef instance = module.make(this.scope.func, builder, structType);

		for (u32_t index = 0; index < structInfo.members.size(); index++)
		{
			var structMember = structInfo.members.at(index);
			var objectMember = node.members.find(structMember.name);
			LLVMValueRef memV = nullptr;
			if (objectMember != node.members.end())
			{
				memV = this.EncodeExpr(objectMember.second);
				if (memV == nullptr)
					return nullptr;
				memV = Model.GetValue(builder, memV);
			}
			else
			{
				memV = structMember.value != nullptr
					? structMember.value
					: module.get_default_value(structMember.type);
			}

			if (memV)
			{
				String memN = String::format("this.%s", structMember.name.cstr());
				LLVMValueRef memP = builder.CreateStructGEP(structType, instance, index, memN.cstr());
				builder.CreateStore(memV, memP);
			}
		}

		return instance;
		*/
		return default;
	}

	LLVMValueRef EncodeExprObjectRef(AstNodeObjectRef node)
	{
		if (node == null)
			return default;
		
		/* TODO:
		var objV = this.EncodeExpr(node.obj);
		var keyV = builder.CreateGlobalString(node.key.cstr());
		if (objV == nullptr || keyV == nullptr)
			return nullptr;

		objV = Model.GetValue(builder, objV);
		var objT = objV.TypeOf();
		if (!(objT.isPointerTy() && objT.getPointerElementType().isStructTy()))
		{
			this.Error("The value is not a object reference.\n");
			return nullptr;
		}

		var structInfo = module.get_struct(objT.getPointerElementType());
		if (structInfo == nullptr)
		{
			this.Error("Can't find the type of this value.\n");
			return nullptr;
		}


		var index = structInfo.get_member_index(node.key);
		if (index == -1)
		{
			this.Error("The object doesn't have a member named '%s'. \n", node.key.cstr());
			return nullptr;
		}

		var structType = structInfo.type;
		LLVMValueRef value = builder.CreateStructGEP(structType, objV, index);
		return value;
		*/
		return default;
	}

	bool EncodeStmt(AstNodeStmt node)
	{
		if (node == null)
			return false;
		
		switch (node.category)
		{
			case AstCategory.STRUCT_DEF:
				return this.EncodeStmtStructDef(node as AstNodeStructDef);
			case AstCategory.ENUM_DEF:
				return this.EncodeStmtEnumDef(node as AstNodeEnumDef);
			case AstCategory.PROC_DEF:
				return this.EncodeStmtProcDef(node as AstNodeProcDef);
			case AstCategory.SYMBOL_DEF:
				return this.EncodeStmtSymbolDef(node as AstNodeSymbolDef);
			case AstCategory.BREAK:
				return this.EncodeStmtBreak(node as AstNodeBreak);
			case AstCategory.CONTINUE:
				return this.EncodeStmtContinue(node as AstNodeContinue);
			case AstCategory.RETURN:
				return this.EncodeStmtReturn(node as AstNodeReturn);
			case AstCategory.IF:
				return this.EncodeStmtIf(node as AstNodeIf);
			case AstCategory.LOOP:
				return this.EncodeStmtLoop(node as AstNodeLoop);
			case AstCategory.BLOCK:
				return this.EncodeStmtBlock(node as AstNodeBlock);
			case AstCategory.ASSIGN:
				return this.EncodeStmtAssign(node as AstNodeAssign);
			case AstCategory.INVOKE:
				return this.EncodeStmtInvoke(node as AstNodeInvoke);
			default:
				return false;
		}

		return false;
	}

	bool EncodeStmtStructDef(AstNodeStmt node)
	{
		if (node == null)
			return false;
		
		/* TODO:
		var thisInstanceInfo = module.new_struct(node.name);

		for ( const var& thisMember: node.members)
		{
			const String &memName = thisMember.name;
			if (thisInstanceInfo.get_member(memName) != nullptr)
			{
				this.Error("The member named '%s' is already exists.\n", memName.cstr());
				return false;
			}

			var memType = this.EncodeType(thisMember.type);
			if (memType == nullptr)
				return false;

			var memValue = this.EncodeExpr(thisMember.value);
			if (memValue == nullptr)
			{
				memValue = module.get_default_value(memType);
			}

			thisInstanceInfo.add_member(memName, memType, memValue);
		}

		thisInstanceInfo.resolve();
		if (!this.scope.addSchema(thisInstanceInfo.name, thisInstanceInfo.type))
		{
			this.Error("There is a same schema named %s in this scope.\n", thisInstanceInfo.name.cstr());
			return false;
		}
		*/
		return true;
	}

	bool EncodeStmtEnumDef(AstNodeEnumDef node)
	{
		if (node == null)
			return false;
		
		String name = node.name;

		/*
		var thisInstanceInfo = module.new_struct(node.name);
		thisInstanceInfo.add_member("value", module.type_i32);
		thisInstanceInfo.resolve();

		const String staticTypePrefix = "$_Static";
		var thisStaticInfo = module.new_struct(staticTypePrefix + node.name);
		for ( const var 
		&thisMember: node.members)
		{
			var memName = thisMember.first;
			if (thisStaticInfo.get_member(memName) != nullptr)
			{
				this.Error("The member named '%s' is already exists.\n", memName.cstr());
				return false;
			}

			var v = builder.getInt32(thisMember.second);
			var o = builder.CreateAlloca(thisInstanceInfo.type);
			var n = String::format("%s.%s", node.name.cstr(), memName.cstr());
			var p = builder.CreateStructGEP(o, 0, n.cstr());
			builder.CreateStore(v, p);
			var memValue = o;

			thisStaticInfo.add_member(memName, thisInstanceInfo.type, memValue);
		}
		thisStaticInfo.resolve();

		if (!this.scope.addSchema(thisStaticInfo.name, thisStaticInfo.type) ||
		    !this.scope.addSchema(thisInstanceInfo.name, thisInstanceInfo.type))
		{
			this.Error("There is a same schema named %s in this scope.\n", thisInstanceInfo.name.cstr());
			return false;
		}

		// make static object
		LLVMValueRef staticV = module.make(this.scope.func, builder, thisStaticInfo.type);
		for (u32_t index = 0; index < thisStaticInfo.members.size(); index++)
		{
			var mem = thisStaticInfo.members.at(index);
			var memV = builder.CreateLoad(mem.value);

			String memN = String::format("%s.%s", name.cstr(), mem.name.cstr());
			LLVMValueRef memP = builder.CreateStructGEP(thisStaticInfo.type, staticV, index, memN.cstr());
			builder.CreateStore(memV, memP);
		}

		if (!this.scope.addSymbol(name, staticV))
		{
			this.Error("There is a same symbol named %s in this scope.\n", name.cstr());
			return false;
		}
		*/
		return true;
	}

	bool EncodeStmtProcDef(AstNodeProcDef node)
	{
		if (node == null)
			return false;
		
		if (this.scope.GetSchema(node.name, false) != null)
			return false;

		var retType = this.EncodeType(node.type);

		List<LLVMTypeRef> argTypes = new List<LLVMTypeRef>();
		foreach (var arg in node.args)
		{
			var argType = this.EncodeType(arg.Value);
			if (argType.Pointer == IntPtr.Zero)
				return false;
			argTypes.Add(argType);
		}

		var procType = LLVM.FunctionType(retType, argTypes.ToArray(), false);
		this.scope.AddSchema(node.name, procType.GetPointerTo());

		return true;
	}

	bool EncodeStmtSymbolDef(AstNodeSymbolDef node)
	{
		if (node == null)
			return false;
		
		if (this.scope.GetSymbol(node.name, false) != null)
		{
			this.Error("The symbol '{0}' is undefined.", node.name);
			return false;
		}

		var type = this.EncodeType(node.type);
		var expr = this.EncodeExpr(node.value);
		if (expr.Pointer == IntPtr.Zero)
			return false;

		var value = Model.GetValue(builder, expr);

		LLVMTypeRef stype = default;
		LLVMTypeRef vtype = value.TypeOf();
		if (type.Pointer != IntPtr.Zero)
		{
			stype = type;
			do
			{
				if (stype.Pointer == vtype.Pointer)
					break;
				if (vtype.CanLosslesslyBitCastTo(stype))
					break;
				if (vtype.IsPointerTy() && vtype.GetPointerElementType().Pointer == stype.Pointer)
				{
					stype = type = vtype;
					break;
				}

				// TODO: 校验类型合法性, 值类型是否遵循标记类型

				this.Error("Type of value can not cast to the type of symbol.\n");
				return false;
			} while (false);
		}
		else
		{
			stype = vtype;
		}

		if (stype.IsVoidTy())
		{
			this.Error("Void-Type can't assign to a symbol.\n");
			return false;
		}
		
		var symbol = LLVM.BuildAlloca(this.builder, stype, "");
		LLVM.BuildStore(this.builder, value, symbol);
		symbol = Model.RefValue(builder, symbol);
		symbol.SetValueName(node.name);

		if (!scope.AddSymbol(node.name, symbol))
		{
			this.Error("There is a symbol named {0} in this scope.", node.name);
			return false;
		}

		return true;
	}

	bool EncodeStmtBreak(AstNodeBreak node)
	{
		if (node == null)
			return false;
		
		if (this.breakingPoint.Pointer == IntPtr.Zero)
			return false;

		LLVM.BuildBr(this.builder, this.breakingPoint);

		return true;
	}

	bool EncodeStmtContinue(AstNodeContinue node)
	{
		if (node == null)
			return false;
		
		if (this.continuePoint.Pointer == IntPtr.Zero)
			return false;

		LLVM.BuildBr(this.builder, this.continuePoint);

		return true;
	}

	bool EncodeStmtReturn(AstNodeReturn node)
	{
		if (node == null)
			return false;
		
		var expectedRetType = LLVM.GetReturnType(this.scope.func.TypeOf());

		if (node.value == null)
		{
			if (!expectedRetType.IsVoidTy())
			{
				this.Error("The function must return a value.\n");
				return false;
			}
			
			LLVM.BuildRetVoid(this.builder);
			return true;
		}

		var expr = this.EncodeExpr(node.value);
		if (expr.Pointer == IntPtr.Zero)
		{
			this.Error("Invalid ret value.\n");
			return false;
		}

		var value = Model.GetValue(builder, expr);
		var actureRetType = value.TypeOf();
		if (actureRetType.Pointer != expectedRetType.Pointer && 
		    !actureRetType.CanLosslesslyBitCastTo(expectedRetType))
		{
			this.Error("The type of return value can't cast to return type of function.\n");
			return false;
		}
		
		LLVM.BuildRet(this.builder, value);

		return true;
	}

	bool EncodeStmtIf(AstNodeIf node)
	{
		if (node == null)
			return false;
		
		var if_true = LLVM.AppendBasicBlock(this.scope.func, "if.true");
		var if_false = LLVM.AppendBasicBlock(this.scope.func, "if.false");
		var if_end = LLVM.AppendBasicBlock(this.scope.func, "if.end");

		var condV = this.EncodeExpr(node.cond);
		if (condV.Pointer == IntPtr.Zero)
			return false;
		condV = Model.GetValue(builder, condV);
		if (!condV.TypeOf().IsIntegerTy(1))
		{
			this.Error("The label 'if.cond' need a bool value.\n");
			return false;
		}

		LLVM.BuildCondBr(this.builder, condV, if_true, if_false);

		// if-true
		LLVM.PositionBuilderAtEnd(this.builder, if_true);
		if (node.branch_true != null)
		{
			if (!this.EncodeStmt(node.branch_true))
				return false;
			var lastBlock = LLVM.GetInsertBlock(this.builder);
			if (lastBlock.Pointer != if_true.Pointer && !Model.IsLastInstATerminator(lastBlock))
			{
				LLVM.BuildBr(this.builder, if_end);
			}
		}
		
		if(!Model.IsLastInstATerminator(if_true))
		{
			LLVM.BuildBr(this.builder, if_end);
		}

		// if-false
		LLVM.PositionBuilderAtEnd(this.builder, if_false);
		if (node.branch_false != null)
		{
			if (!this.EncodeStmt(node.branch_false))
				return false;
			var lastBlock = LLVM.GetInsertBlock(this.builder);
			if (lastBlock.Pointer != if_false.Pointer && !Model.IsLastInstATerminator(lastBlock))
			{
				LLVM.BuildBr(this.builder, if_end);
			}
		}
		
		if(!Model.IsLastInstATerminator(if_false))
		{
			LLVM.BuildBr(this.builder, if_end);
		}

		LLVM.PositionBuilderAtEnd(this.builder, if_end);

		return true;
	}

	bool EncodeStmtLoop(AstNodeLoop node)
	{
		if (node == null)
			return false;
		
		this.PushScope(this.scope.func);

		var loop_cond = LLVM.AppendBasicBlock(this.scope.func, "loop.cond");
		var loop_step = LLVM.AppendBasicBlock(this.scope.func, "loop.step");
		var loop_body = LLVM.AppendBasicBlock(this.scope.func, "loop.body");
		var loop_end = LLVM.AppendBasicBlock(this.scope.func, "loop.end");

		var oldContinuePoint = this.continuePoint;
		var oldBreakingPoint = this.breakingPoint;
		this.continuePoint = loop_step;
		this.breakingPoint = loop_end;

		if (!this.EncodeStmt(node.init))
			return false;
		LLVM.BuildBr(this.builder, loop_cond);

		LLVM.PositionBuilderAtEnd(this.builder, loop_cond);
		var condV = this.EncodeExpr(node.cond);
		if (condV.Pointer == IntPtr.Zero)
			return false;
		condV = Model.GetValue(builder, condV);
		if (!condV.TypeOf().IsIntegerTy(1))
		{
			this.Error("The label 'for.cond' need a bool value.\n");
			return false;
		}

		LLVM.BuildCondBr(this.builder, condV, loop_body, loop_end);

		LLVM.PositionBuilderAtEnd(this.builder, loop_body);
		if (node.body != null)
		{
			if (!this.EncodeStmt(node.body))
				return false;
			if (!Model.IsLastInstATerminator(loop_body))
			{
				LLVM.BuildBr(this.builder, loop_step);
				
			}
		}
		
		if(!Model.IsLastInstATerminator(loop_body))
		{
			LLVM.BuildBr(this.builder, loop_step);
		}

		LLVM.PositionBuilderAtEnd(this.builder, loop_step);
		if (!this.EncodeStmt(node.step))
			return false;
		LLVM.BuildBr(this.builder, loop_cond);

		LLVM.PositionBuilderAtEnd(this.builder, loop_end);

		this.continuePoint = oldContinuePoint;
		this.breakingPoint = oldBreakingPoint;

		this.PopScope();

		return true;
	}

	bool EncodeStmtBlock(AstNodeBlock node)
	{
		if (node == null)
			return false;
		
		this.PushScope(this.scope.func);

		foreach (var stmt in node.stmts)
		{
			if (!this.EncodeStmt(stmt))
				return false;
		}

		this.PopScope();

		return true;
	}

	bool EncodeStmtAssign(AstNodeAssign node)
	{
		if (node == null)
			return false;
		
		var left = this.EncodeExpr(node.left);
		var right = this.EncodeExpr(node.right);
		if (left.Pointer == IntPtr.Zero || right.Pointer == IntPtr.Zero)
			return false;

		var ptr = Model.RefValue(builder, left);
		var val = Model.GetValue(builder, right);
		LLVM.BuildStore(this.builder, val, ptr);

		return true;
	}

	bool EncodeStmtInvoke(AstNodeInvoke node)
	{
		if (node == null)
			return false;
		var expr = this.EncodeExprFuncRef(node.expr as AstNodeFuncRef);
		return expr.Pointer != IntPtr.Zero;
	}

	private void Error(string fmt, params object[] args)
	{
		this.error = string.Format(fmt, args);
	}

	private void ErrorSymbolIsUndefined(string name)
	{
		this.Error("The symbol '{0}' is undefined.", name);
	}

	private void ErrorSymbolIsAlreadyDefined(string name)
	{
		this.Error("The symbol '{0}' is defined already.");
	}
}
