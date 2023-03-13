using LLVMSharp;

namespace Eokas;

public static class LLVMExtensions
{
    public static bool IsVoidTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMVoidTypeKind;
    }
    
    public static bool IsIntegerTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind;
    }
    
    public static bool IsIntegerTy(this LLVMTypeRef type, int bitwidth)
    {
        return type.TypeKind == LLVMTypeKind.LLVMIntegerTypeKind &&
               type.GetIntTypeWidth() == bitwidth;
    }

    public static bool IsFloatingPointTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMFloatTypeKind;
    }

    public static bool IsPointerTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMPointerTypeKind;
    }

    public static bool IsFunctionTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMFunctionTypeKind;
    }

    public static bool IsStructTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMStructTypeKind;
    }

    public static bool IsArrayTy(this LLVMTypeRef type)
    {
        return type.TypeKind == LLVMTypeKind.LLVMArrayTypeKind;
    }

    public static LLVMTypeRef GetPointerTo(this LLVMTypeRef type)
    {
        return LLVM.PointerType(type, 0);
    }

    public static LLVMTypeRef GetPointerElementType(this LLVMTypeRef type)
    {
        return LLVM.GetElementType(type);
    }
}