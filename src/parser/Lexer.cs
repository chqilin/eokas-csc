namespace Eokas;

class CharUtils
{
	public static bool IsNumber(char c)
	{
		return char.IsDigit(c);
	}

	public static bool IsAlpha(char c)
	{
		return char.IsAsciiLetter(c);
	}

	public static bool IsAlpha_(char c)
	{
		return char.IsAsciiLetter(c) || c == '_';
	}

	public static bool IsAlphaNumber_(char c)
	{
		return char.IsAsciiLetterOrDigit(c) || c == '_';
	}

	public static bool IsHex(char c)
	{
		return char.IsAsciiHexDigit(c);
	}
}

public class Lexer
{
	public string source { get; private set; } = "";
    public int position { get; private set; } = 0;
    public char current { get; private set; } = '\0';
    public int line { get; private set; } = 0;
    public int column { get; private set; } = 0;

    private Token mToken;
    private Token mLookAheadToken;
    
	~Lexer()
	{
		this.Clear();
	}
	
	public void Clear()
	{
		this.source = "";
		this.position = 0;
		this.current = '\0';
		this.line = 1;
		this.column = 0;
		
		this.mToken.Clear();
		this.mLookAheadToken.Clear();
	}
	
	public void Ready(string source)
	{
		this.Clear();
		this.source = source;
		this.position = 0;
		this.ReadChar();
	}

	public void NextToken()
	{
		if(this.mLookAheadToken.type != Token.Type.UNKNOWN)
		{
			this.mToken = this.mLookAheadToken;
			this.mLookAheadToken.Clear();
		}
		else
		{
			this.Scan();
		}
	}

	public Token token
	{
		get { return this.mToken; }
	}
	
	public Token lookAheadToken
	{
		get
		{
			if (this.mLookAheadToken.type == Token.Type.UNKNOWN)
			{
				Token tmp = this.mToken;
				this.Scan();
				this.mLookAheadToken = this.mToken;
				this.mToken = tmp;
			}

			return this.lookAheadToken;
		}
	}
	
	void Scan()
	{
		this.mToken.Clear();
		
		for (;;)
		{
			switch (this.current)
			{
				case '\0':
					this.mToken.type = Token.Type.EOS;
					return;
				
				case '\n':
				case '\r':
					this.NewLine();
					break;
				
				case ' ':
				case '\f':
				case '\t':
				case '\v': // spaces
					this.ReadChar();
					break;
				
				case '/':    // '//' '/*' '/'
				{
					char div = this.current;
					this.ReadChar();
					if(this.current == '/') // line comment
					{
						this.ScanLineComment();
						break;
					}
					else if(this.current == '*') // section comment
					{
						this.ScanSectionComment();
						break;
					}
					
					this.SaveChar(div);
					this.mToken.type = Token.Type.DIV;
					return;
				}
				
				case '&': // && or &
					this.SaveAndReadChar();
					if(this.current == '&')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.AND2;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.AND;
						return;
					}
				
				case '|': // ||, |< or |
					this.SaveAndReadChar();
					if(this.current == '|')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.OR2;
						return;
					}
					else if(this.current == '<')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.SHIFT_L;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.OR;
						return;
					}
				
				case '=': // == or =
					this.SaveAndReadChar();
					if(this.current == '=')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.EQ;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.ASSIGN;
						return;
					}
				
				case '<': // <=, <
					this.SaveAndReadChar();
					if(this.current == '=')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.LE;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.LT;
						return;
					}
				
				case '>': // >|, >= or >
					this.SaveAndReadChar();
					if(this.current == '|')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.SHIFT_R;
						return;
					}
					else if(this.current == '=')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.GE;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.GT;
						return;
					}
				
				case '!': // != or !
					this.SaveAndReadChar();
					if(this.current == '=')
					{
						this.SaveAndReadChar();
						this.mToken.type = Token.Type.NE;
						return;
					}
					else
					{
						this.mToken.type = Token.Type.NOT;
						return;
					}
				
				case '.':
					this.SaveAndReadChar();
					if(this.current == '.')
					{
						this.SaveAndReadChar();
						if(this.current == '.')
						{
							this.SaveAndReadChar();
							this.mToken.type = Token.Type.DOT3;
							return;
						}
						this.mToken.type = Token.Type.DOT2;
						return;
					}
					this.mToken.type = Token.Type.DOT;
					return;
				
				case '0':
				case '1':
				case '2':
				case '3':
				case '4':
				case '5':
				case '6':
				case '7':
				case '8':
				case '9':
					this.ScanNumber();
					return;
				
				case '"':
				case '\'':
					this.ScanString(this.current);
					return;
				default:
					if(CharUtils.IsAlpha_(this.current))
					{
						this.ScanIdentifier();
						return;
					}
					else
					{
						// single operator + - * / etc.
						this.SaveAndReadChar();
						this.mToken.Infer(Token.Type.UNKNOWN);
						return;
					}
			}
		}
	}
	
	void ScanNumber()
	{
		this.SaveAndReadChar();
		
		if(this.current == 'b' || this.current == 'B') // bit
		{
			this.SaveAndReadChar();
			while (this.current == '0' || this.current == '1')
			{
				this.SaveAndReadChar();
			}
			this.mToken.type = Token.Type.INT_B;
		}
		else if(this.current == 'x' || this.current == 'X') // hex
		{
			this.SaveAndReadChar();
			while (CharUtils.IsHex(this.current))
			{
				this.SaveAndReadChar();
			}
			this.mToken.type = Token.Type.INT_X;
		}
		else // decimal
		{
			while (CharUtils.IsNumber(this.current))
			{
				this.SaveAndReadChar();
			}
			this.mToken.type = Token.Type.INT_D;
			
			if(this.current == '.')
			{
				this.SaveAndReadChar();
				while (CharUtils.IsNumber(this.current))
				{
					this.SaveAndReadChar();
				}
				this.mToken.type = Token.Type.FLOAT;
			}
		}
	}
	
	void ScanString(char delimiter)
	{
		this.ReadChar();
		while (this.current != delimiter)
		{
			if(this.current == '\0')
			{
				this.mToken.type = Token.Type.UNKNOWN;
				return;
			}
			else if(this.current == '\n' || this.current == '\r')
			{
				this.mToken.type = Token.Type.UNKNOWN;
				return;
			}
			else if(this.current == '\\') // eseokas sequence
			{
				this.ReadChar();
				switch (this.current)
				{
					case 'a':
						this.SaveChar('\a');
						this.ReadChar();
						break;
					case 'b':
						this.SaveChar('\b');
						this.ReadChar();
						break;
					case 'f':
						this.SaveChar('\f');
						this.ReadChar();
						break;
					case 'n':
						this.SaveChar('\n');
						this.ReadChar();
						break;
					case 'r':
						this.SaveChar('\r');
						this.ReadChar();
						break;
					case 't':
						this.SaveChar('\t');
						this.ReadChar();
						break;
					case 'v':
						this.SaveChar('\v');
						this.ReadChar();
						break;
					case 'x': // \xFF
						this.ReadChar();
						if(!CharUtils.IsHex(this.current))
						{
							this.mToken.type = Token.Type.UNKNOWN;
							return;
						}
						this.SaveAndReadChar();
						if(!CharUtils.IsHex(this.current))
						{
							this.mToken.type = Token.Type.UNKNOWN;
							return;
						}
						this.SaveAndReadChar();
						break;
					case '\\':
					case '\'':
					case '"':
						this.SaveAndReadChar();
						break;
					default:
						this.mToken.type = Token.Type.UNKNOWN;
						return;
				}
			}
			else
			{
				this.SaveAndReadChar();
			}
		}
		this.ReadChar();
		this.mToken.type = Token.Type.STRING;
	}
	
	void ScanIdentifier()
	{
		this.SaveAndReadChar();
		while(CharUtils.IsAlphaNumber_(this.current))
		{
			this.SaveAndReadChar();
		}
		this.mToken.Infer(Token.Type.ID);
	}
	
	void ScanLineComment()
	{
		this.ReadChar();
		while (this.current != '\n' && this.current != '\r' && this.current != '\0')
			this.ReadChar(); // skip to end-of-line or end-of-source
	}
	
	void ScanSectionComment()
	{
		this.ReadChar();
		for (;;)
		{
			switch (this.current)
			{
				case '\0':
					this.mToken.type = Token.Type.EOS;
					return;
				
				case '\n':
				case '\r':
					this.NewLine();
					break;
				
				case '*':
					this.ReadChar();
					if(this.current == '/')
					{
						this.ReadChar(); // skip /
						return;
					}
					else
					{
						break;
					}
				default:
					this.ReadChar();
					break;
			}
		}
	}
	
	void NewLine()
	{
		char old = this.current;
		this.ReadChar();
		if(this.current == '\n' || (this.current == '\r' && this.current != old))
			this.ReadChar();
		this.line++;
		this.column = 0;
	}
	
	void ReadChar()
	{
		this.current = this.source[this.position];
		this.position++;
		this.column++;
	}
	
	void SaveChar(char c)
	{
		this.mToken.value += c;
	}
	
	void SaveAndReadChar()
	{
		this.SaveChar(this.current);
		this.ReadChar();
	}
	
	bool checkChar(string charset)
	{
		return charset.IndexOf(this.current) >= 0;
	}
}