enum SMDToken
{
    NAME,
    NUMBER,
    END,
    COMMAND = '#',
    LBRACKET = '{',
    RBRACKET = '}',
    COMMA = ',',
    SEMICOLON = ';',
    SMD_ERROR
};

static FILE* SMDFile;
static float    TokenNumber;
static char     TokenString[256];
static SMDToken CurrentToken;

static SMDToken GetToken()
{
    char ch;
    TokenString[0] = '\0';
    do
    {
        if ((ch = (char)fgetc(SMDFile)) == EOF) return END;
        if (ch == '/' && (ch = (char)fgetc(SMDFile)) == '/')
        {
            int commentChar;
            while ((commentChar = fgetc(SMDFile)) != '\n' && commentChar != EOF)
            {
                // skip the rest of the comment line
            }
            ch = (commentChar == EOF) ? static_cast<char>(EOF) : '\n';
            if (commentChar == EOF)
            {
                return END;
            }
        }
    } while (isspace(ch));

    char* p, TempString[100];
    switch (ch)
    {
    case '#':
        return CurrentToken = COMMAND;
    case ';':
        return CurrentToken = SEMICOLON;
    case ',':
        return CurrentToken = COMMA;
    case '{':
        return CurrentToken = LBRACKET;
    case '}':
        return CurrentToken = RBRACKET;
    case '0':	case '1':	case '2':	case '3':	case '4':
    case '5':	case '6':	case '7':	case '8':	case '9':
    case '.':	case '-':
        {
        ungetc(ch, SMDFile);
        p = TempString;
        char* const tempStringEnd = TempString + sizeof(TempString) - 1;
        while (p < tempStringEnd
               && ((ch = (char)getc(SMDFile)) != EOF)
               && (ch == '.' || isdigit(ch) || ch == '-'))
        {
            *p++ = ch;
        }
        *p = 0;
        if (p == tempStringEnd)
        {
            while (((ch = (char)getc(SMDFile)) != EOF)
                   && (ch == '.' || isdigit(ch) || ch == '-'))
            {
                // discard the remaining digits of an over-long number
            }
        }
        
        TokenNumber = (float)atof(TempString);
        //			sscanf(TempString," %f ",&TokenNumber);
        return CurrentToken = NUMBER;
        }
    case '"':
        p = TokenString;
        {
            char* const tokenEnd = TokenString + sizeof(TokenString) - 1;
            while (p < tokenEnd && ((ch = (char)getc(SMDFile)) != EOF) && ch != '"')
            {
                *p++ = ch;
            }
            if (p == tokenEnd && ch != '"')
            {
                while (((ch = (char)getc(SMDFile)) != EOF) && ch != '"')
                {
                    // discard over-long quoted input
                }
            }
        }
        if (ch != '"')
            ungetc(ch, SMDFile);
        *p = 0;
        return CurrentToken = NAME;
    default:
        if (isalpha(ch))
        {
            p = TokenString;
            char* const tokenEnd = TokenString + sizeof(TokenString) - 1;
            *p++ = ch;
            while (p < tokenEnd
                   && ((ch = (char)getc(SMDFile)) != EOF)
                   && (ch == '.' || ch == '_' || isalnum(ch)))
            {
                *p++ = ch;
            }
            if (p == tokenEnd)
            {
                while (((ch = (char)getc(SMDFile)) != EOF)
                       && (ch == '.' || ch == '_' || isalnum(ch)))
                {
                    // discard over-long identifier
                }
            }
            ungetc(ch, SMDFile);
            *p = 0;
            return CurrentToken = NAME;
        }
        return CurrentToken = SMD_ERROR;
    }
}