namespace Eokas;

using System;

public struct Token
{
    public enum Type
    {
        USING,
        VAR, VAL, MAKE,
        FUNC, PROC, STRUCT, ENUM,
        IF, ELSE, LOOP,  BREAK, CONTINUE, RETURN, TRUE, FALSE,
        COMMA, SEMICOLON, COLON, QUESTION, AT, POUND, DOLLAR,
        ADD, SUB, MUL, DIV, MOD, XOR, FLIP,
        LRB, RRB, LSB, RSB, LCB, RCB,
        AND, AND2, OR, OR2,
        ASSIGN, EQ, NOT, NE, GT, GE, LT, LE,
        SHIFT_L, SHIFT_R,
        DOT, DOT2, DOT3,
        INT_B, INT_X, INT_D, FLOAT, STRING, ID, EOS, COUNT, UNKNOWN
    }
    
    public static string[] names = new string[(int)Type.COUNT]
    {
        "using",
        "var", "val", "make",
        "func", "proc", "struct", "enum",
        "if", "else", "loop", "break", "continue", "return", "true", "false",
        ",", ";", ":", "?", "@", "#", "$",
        "+", "-", "*", "/", "%", "^", "~",
        "(", ")", "[", "]", "{", "}",
        "&", "&&", "|", "||",
        "=", "==", "!", "!=", ">", ">=", "<", "<=",
        ">|", "|<",
        ".", "..", "...",
        "<b-int>", "<x-int>", "<d-int>", "<float>", "<string>", "<identifier>", "<eos>"
    };
    
    public Type type;
    public String value;
    
    public string name
    {
        get
        {
            if (this.type < Type.COUNT)
                return names[(int)this.type];
            return "";
        }
    }
	
    public void Clear()
    {
        this.type = Type.UNKNOWN;
        this.value = "";
    }
    
    public bool Infer(Type defaultType)
    {
        bool found = false;
        int count = (int) (Type.COUNT);
        for (int i = 0; i < count; i++)
        {
            if(this.value == Token.names[i])
            {
                this.type = (Type)i;
                found = true;
                break;
            }
        }
        
        if(!found)
        {
            this.type = defaultType;
        }
        
        return found;
    }
}
