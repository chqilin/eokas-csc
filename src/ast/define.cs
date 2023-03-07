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
    MAX_PRIORITY = 800,
    UNKNOWN = 0x7FFFFFFF
}
	
enum AstUnaryOper
{
    POS = 900, NEG, FLIP, SIZE_OF, TYPE_OF,
    NOT = 1000,
    MAX_PRIORITY = 1100,
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
