using System.Text; // For StringBuilder

public class LispInterpreter
{
    public class Environment
    {
        private readonly Dictionary<string, int> _bindings;
        private readonly Environment? _parent;

        public Environment()
        {
            _bindings = new Dictionary<string, int>();
            _parent = null;
        }

        public Environment(Environment parent)
        {
            _bindings = new Dictionary<string, int>();
            _parent = parent;
        }

        public void Define(string name, int value)
        {
            _bindings[name] = value;
        }

        public int Lookup(string name)
        {
            if (_bindings.ContainsKey(name))
            {
                return _bindings[name];
            }
            else if (_parent != null)
            {
                return _parent.Lookup(name);
            }
            else
            {
                throw new Exception($"Evaluation Error: Undefined variable: {name}");
            }
        }
    }

    public enum TokenType
    {
        OpenParen,
        CloseParen,
        Identifier,
        Integer,
        Space,
        EndOfFile,
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Value { get; }

        public Token(TokenType type, string value = "")
        {
            Type = type;
            Value = value;
        }
    }

    public static int EvaluateLispExpression(string input)
    {
        int _position = 0;
        Token _currentToken;
        string _inputString = input;
        char PeekChar()
        {
            if (_position >= _inputString.Length)
                return '\0';
            return _inputString[_position];
        }
        char ConsumeChar()
        {
            if (_position >= _inputString.Length)
                return '\0';
            return _inputString[_position++];
        }
        Token GetNextToken()
        {
            if (_position >= _inputString.Length)
                return new Token(TokenType.EndOfFile);
            char currentChar = PeekChar();
            if (currentChar == ' ')
            {
                ConsumeChar();
                return new Token(TokenType.Space);
            }
            if (currentChar == '(')
            {
                ConsumeChar();
                return new Token(TokenType.OpenParen);
            }
            if (currentChar == ')')
            {
                ConsumeChar();
                return new Token(TokenType.CloseParen);
            }
            if (
                char.IsDigit(currentChar)
                || (
                    currentChar == '-'
                    && _position + 1 < _inputString.Length
                    && char.IsDigit(_inputString[_position + 1])
                )
            )
            {
                StringBuilder sb = new StringBuilder();
                if (currentChar == '-')
                {
                    sb.Append(ConsumeChar());
                    currentChar = PeekChar();
                }
                while (_position < _inputString.Length && char.IsDigit(currentChar))
                {
                    sb.Append(ConsumeChar());
                    currentChar = PeekChar();
                }
                return new Token(TokenType.Integer, sb.ToString());
            }

            if (char.IsLetter(currentChar))
            {
                StringBuilder sb = new StringBuilder();
                while (_position < _inputString.Length && (char.IsLetterOrDigit(currentChar)))
                {
                    sb.Append(ConsumeChar());
                    currentChar = PeekChar();
                }
                return new Token(TokenType.Identifier, sb.ToString());
            }

            throw new Exception(
                $"Syntax Error (Tokenizer): Unexpected character: '{currentChar}' at position {_position}"
            );
        }

        Token PeekNextToken()
        {
            int originalPosition = _position;
            Token tempCurrentToken = _currentToken; // Store _currentToken state

            // Advance tokenizer to get the next token
            _currentToken = GetNextToken();
            Token nextToken = _currentToken;

            // Rollback tokenizer state
            _position = originalPosition;
            _currentToken = tempCurrentToken;

            return nextToken;
        }

        // Helper to "eat" a token of expected type
        void Eat(TokenType type)
        {
            if (_currentToken.Type == type)
            {
                _currentToken = GetNextToken();
            }
            else
            {
                throw new Exception(
                    $"Syntax Error (Parser): Expected token type {type}, but got {_currentToken.Type} ('{_currentToken.Value}') "
                );
            }
        }

        // Helper to "eat" a space token
        void EatSpace()
        {
            if (_currentToken.Type == TokenType.Space)
            {
                Eat(TokenType.Space);
            }
        }

        // Recursive function to parse and evaluate an expression directly
        // It takes the current environment as an argument
        object ParseAndEvaluateExpression(Environment currentEnv)
        {
            switch (_currentToken.Type)
            {
                case TokenType.Integer:
                    int value = int.Parse(_currentToken.Value);
                    Eat(TokenType.Integer);
                    return value; // Return raw int for evaluation

                case TokenType.Identifier:
                    string name = _currentToken.Value;
                    Eat(TokenType.Identifier);
                    return currentEnv.Lookup(name); // Directly evaluate variable lookup

                case TokenType.OpenParen:
                    Eat(TokenType.OpenParen); // Consume '('

                    if (_currentToken.Type != TokenType.Identifier)
                    {
                        throw new Exception(
                            "Syntax Error: Expected an operator or keyword after '('."
                        );
                    }
                    string operatorOrKeyword = _currentToken.Value;
                    Eat(TokenType.Identifier); // Consume keyword (add, mult, let)

                    // Immediately dispatch based on operator/keyword
                    switch (operatorOrKeyword)
                    {
                        case "add":
                            EatSpace();
                            int leftAdd = (int)ParseAndEvaluateExpression(currentEnv); // Recursively evaluate left
                            EatSpace();
                            int rightAdd = (int)ParseAndEvaluateExpression(currentEnv); // Recursively evaluate right
                            Eat(TokenType.CloseParen); // Consume ')'
                            return leftAdd + rightAdd;

                        case "mult":
                            EatSpace();
                            int leftMult = (int)ParseAndEvaluateExpression(currentEnv); // Recursively evaluate left
                            EatSpace();
                            int rightMult = (int)ParseAndEvaluateExpression(currentEnv); // Recursively evaluate right
                            Eat(TokenType.CloseParen); // Consume ')'
                            return leftMult * rightMult;

                        case "let":
                            // For 'let', we need a new environment for its scope
                            Environment letScopeEnv = new Environment(currentEnv);
                            EatSpace();
                            while (true)
                            {
                                if (_currentToken.Type != TokenType.Identifier)
                                {
                                    break;
                                }
                                Token nextToken = PeekNextToken();
                                if (nextToken.Type == TokenType.CloseParen)
                                {
                                    break; // Break the binding loop; current Identifier is the body
                                }
                                // If the next token is an Identifier, it's a variable binding
                                string varName = _currentToken.Value;
                                Eat(TokenType.Identifier); // Consume var name
                                EatSpace(); // Space after var name

                                // Evaluate the value expression for the binding in the *current* let scope
                                // This is crucial for sequential (let*) behavior
                                int boundValue = (int)ParseAndEvaluateExpression(letScopeEnv);
                                letScopeEnv.Define(varName, boundValue); // Define in the new let environment

                                // If the next token is NOT a CloseParen, expect a space before the next element
                                if (_currentToken.Type != TokenType.CloseParen)
                                {
                                    EatSpace();
                                }
                            }
                            // Parse and evaluate the body expression in the final let scope
                            int bodyResult = (int)ParseAndEvaluateExpression(letScopeEnv);
                            Eat(TokenType.CloseParen); // Consume ')' for the 'let' expression
                            return bodyResult;

                        default:
                            throw new Exception(
                                $"Evaluation Error: Unknown operator or keyword '{operatorOrKeyword}'."
                            );
                    }

                case TokenType.EndOfFile:
                    throw new Exception("Syntax Error: Unexpected end of input.");

                default:
                    throw new Exception(
                        $"Syntax Error: Unexpected token type: {_currentToken.Type} ('{_currentToken.Value}') at position {_position})"
                    );
            }
        }
        _currentToken = GetNextToken();
        if (_currentToken.Type == TokenType.Space)
        {
            Eat(TokenType.Space); // Consume any leading space
        }
        // Create the initial global environment
        Environment globalEnv = new Environment();
        // Start the recursive parse-and-evaluate process
        object finalResult = ParseAndEvaluateExpression(globalEnv);

        // Ensure no extra tokens remain after the main expression
        if (_currentToken.Type != TokenType.EndOfFile)
        {
            throw new Exception("Syntax Error: Extra characters at end of input.");
        }
        // Return the final integer result
        if (finalResult is int intValue)
        {
            return intValue;
        }
        else
        {
            throw new Exception(
                $"Evaluation Error: Final expression did not evaluate to an integer. Result was: {finalResult?.GetType().Name}"
            );
        }
    }

    // --- Main Program for Testing ---
    public static void Main(string[] args)
    {
        Console.WriteLine("--- Lisp Interpreter Demo ---");

        RunTest("(let x 2 y 3 x (mult x y) (add x y))"); // Expected: 5
        RunTest("(let x 2 y 3 (add x (let x 4 (add x y))))"); // Expected: 9
        RunTest("(add (mult 2 3) (let a 5 (add a 1)))"); // Expected: 12
        RunTest("(let x 3 (let x 2 x))"); // Expected: 2
        RunTest("(let x 3 x)"); // Expected: 2
        RunTest("(add 10 20)"); // Expected: 30
        RunTest("42"); // Expected: 42
        RunTest("(let x 10 y (add x 5) (mult x y))"); // Expected: 150

        // Error cases
        RunTest("(add x 5)"); // Expected: Error (Undefined variable: x)
        RunTest("()"); // Expected: Error
        RunTest("(mult 1 2 3)"); // Expected: Error (Parser: expects only 2 args)
        RunTest("(let x 10 y)"); // Expected: Error (Parser: malformed let)
        RunTest("(+ 1 2)"); // Expected: Error (Unknown operator: +)
    }

    public static void RunTest(string input)
    {
        Console.WriteLine($"\nEvaluating: \"{input}\"");
        try
        {
            int result = EvaluateLispExpression(input);
            Console.WriteLine($"Result: {result}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error: {e.Message}");
        }
    }
}
