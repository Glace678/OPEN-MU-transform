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
    // Read characters as int: fgetc/getc return either a byte 0..255 or EOF (-1).
    // Holding them in a plain char before the EOF test is wrong - with a signed char a
    // 0xFF byte looks like EOF, and with an unsigned char a real EOF never equals -1 and
    // loops forever. Compare as int first, then narrow to char.
    int ch;
    char c;
    TokenString[0] = '\0';
    do
    {
        if ((ch = fgetc(SMDFile)) == EOF) return END;
        if (ch == '/' && (ch = fgetc(SMDFile)) == '/')
        {
            int commentChar;
            while ((commentChar = fgetc(SMDFile)) != '\n' && commentChar != EOF)
            {
                // skip the rest of the comment line
            }
            c = (commentChar == EOF) ? static_cast<char>(EOF) : '\n';
            if (commentChar == EOF)
            {
                return END;
            }
            ch = static_cast<int>(static_cast<unsigned char>(c));
        }
    } while (isspace(static_cast<unsigned char>(ch)));

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
        c = static_cast<char>(ch);
        ungetc(c, SMDFile);
        p = TempString;
        char* const tempStringEnd = TempString + sizeof(TempString) - 1;
        while (p < tempStringEnd
               && ((ch = getc(SMDFile)) != EOF)
               && ((c = static_cast<char>(ch)) == '.' || isdigit(static_cast<unsigned char>(c)) || c == '-'))
        {
            *p++ = c;
        }
        *p = 0;
        if (p == tempStringEnd)
        {
            while (((ch = getc(SMDFile)) != EOF)
                   && (((c = static_cast<char>(ch)) == '.') || isdigit(static_cast<unsigned char>(c)) || c == '-'))
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
            while (p < tokenEnd && ((ch = getc(SMDFile)) != EOF) && ch != '"')
            {
                *p++ = static_cast<char>(ch);
            }
            if (p == tokenEnd && ch != '"')
            {
                while (((ch = getc(SMDFile)) != EOF) && ch != '"')
                {
                    // discard over-long quoted input
                }
            }
        }
        if (ch != '"')
            ungetc(static_cast<char>(ch), SMDFile);
        *p = 0;
        return CurrentToken = NAME;
    default:
        c = static_cast<char>(ch);
        if (isalpha(static_cast<unsigned char>(c)))
        {
            p = TokenString;
            char* const tokenEnd = TokenString + sizeof(TokenString) - 1;
            *p++ = c;
            while (p < tokenEnd
                   && ((ch = getc(SMDFile)) != EOF)
                   && ((c = static_cast<char>(ch)) == '.' || c == '_' || isalnum(static_cast<unsigned char>(c))))
            {
                *p++ = c;
            }
            if (p == tokenEnd)
            {
                while (((ch = getc(SMDFile)) != EOF)
                       && ((c = static_cast<char>(ch)) == '.' || c == '_' || isalnum(static_cast<unsigned char>(c))))
                {
                    // discard over-long identifier
                }
            }
            ungetc(c, SMDFile);
            *p = 0;
            return CurrentToken = NAME;
        }
        return CurrentToken = SMD_ERROR;
    }
}