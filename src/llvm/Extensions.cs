using LLVMSharp;

namespace Eokas;

public static class LLVMExtensions
{
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
}