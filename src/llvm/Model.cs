using LLVMSharp;

namespace Eokas;

public class Model
{
    public static LLVMTypeRef TyVoid = LLVM.VoidType();
    public static LLVMTypeRef TyI8 = LLVM.Int8Type();
    public static LLVMTypeRef TyI16 = LLVM.Int16Type();
    public static LLVMTypeRef TyI32 = LLVM.Int32Type();
    public static LLVMTypeRef TyI64 = LLVM.Int64Type();
    public static LLVMTypeRef TyF32 = LLVM.FloatType();
    public static LLVMTypeRef TyF64 = LLVM.DoubleType();
    public static LLVMTypeRef TyBool = LLVM.Int1Type();
    public static LLVMTypeRef TyBytePtr = LLVM.PointerType(LLVM.Int8Type(), 0);

    public static LLVMValueRef DeclareFunction(LLVMModuleRef module, String name, LLVMTypeRef ret, LLVMTypeRef[] args, bool varg)
    {
        var funcType = LLVM.FunctionType(ret, args, varg);
        var funcValue = LLVM.AddFunction(module, name, funcType);
        return funcValue;
    }
    
    public static LLVMValueRef DefineFunction(LLVMModuleRef module, String name, LLVMTypeRef ret, LLVMTypeRef[] args, bool varg, Action<LLVMModuleRef, LLVMValueRef, LLVMBuilderRef> body)
    {
        var funcType = LLVM.FunctionType(ret, args, varg);
        var funcValue = LLVM.AddFunction(module, name, funcType);
        var builder = LLVM.CreateBuilder();
        body(module, funcValue, builder);
		
        return funcValue;
    }
    
    public static LLVMValueRef GetDefaultValue(LLVMTypeRef type)
    {
        if(type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind)
        {
            return LLVM.ConstInt(type, 0, false);
        }
        if(type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind)
        {
            return LLVM.ConstReal(type, 0.0);
        }
        return LLVM.ConstPointerNull(type);
    }
    
    /**
     * For ref-types, transform multi-level pointer to one-level pointer.
     * For val-types, transform multi-level pointer to real value.
     * */
    public static LLVMValueRef GetValue(LLVMBuilderRef builder, LLVMValueRef value)
    {
        var type = value.TypeOf();
        
        while (type.TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            if (type.GetElementType().TypeKind == LLVMTypeKind.LLVMFunctionTypeKind)
                break;
            if (type.GetElementType().TypeKind == LLVMTypeKind.LLVMStructTypeKind)
                break;
            if (type.GetElementType().TypeKind == LLVMTypeKind.LLVMArrayTypeKind)
                break;
            value = LLVM.BuildLoad(builder, value, "");
            type = value.TypeOf();
        }
        
        return value;
    }
	
    /**
	 * Transform the multi-level pointer value to one-level pointer type value.
	 * Ignores literal values.
	 * */
    public static LLVMValueRef RefValue(LLVMBuilderRef builder, LLVMValueRef value)
    {
        var type = value.TypeOf();
		
        while (type.TypeKind == LLVMTypeKind.LLVMPointerTypeKind && 
               type.GetElementType().TypeKind == LLVMTypeKind.LLVMPointerTypeKind)
        {
            value = LLVM.BuildLoad(builder, value, "");
            type = value.TypeOf();
        }
		
        return value;
    }
}