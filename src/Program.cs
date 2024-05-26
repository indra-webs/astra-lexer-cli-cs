using System.Text;

using Indra.Astra.CLI;

using Meep.Tech.Text;

using static Indra.Astra.Lexer;

using Console = System.Console;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Style",
    "IDE1006:Naming Styles",
    Justification = "Static Program Class")]
static class Program {
    public const string ShortPlainFlag = "-p";
    public const string LongPlainFlag = "--plain";
    public const string ShortHelpFlag = "-h";
    public const string LongHelpFlag = "--help";

    public static readonly (string Short, string Long) PlainFlag
        = (ShortPlainFlag, LongPlainFlag);
    public static readonly (string Short, string Long) HelpFlag
        = (ShortHelpFlag, LongHelpFlag);

    public const string HelpDescription = $"""
        Astra Lexer CLI.
        ============================
        > A command line interface for lexing the Astra Programming Language.

        ----------------------------
        *Usage*: {Usage}

        ----------------------------
        *Flags*:
            [{ShortPlainFlag}|{LongPlainFlag}]: (Optional) Run the script without colorizing the output.
            [{ShortHelpFlag} |{LongHelpFlag}]:  (Optional) Display this help message.

        ----------------------------
        *Params*:
            [script]: (Optional) The script to be lexed. If not provided, an input loop will be started to read the script from the console. You can add a newline using Shift+Enter or exit the input loop using ESC.

        ----------------------------
        **Keys**: (Input Loop Only)
            - Enter: Lex the current script.
            - Shift+Enter: Insert a new line into the current script.
            - ESC: Exit the input loop. (Exiting the program with the terminal shortcut (usually Ctrl+C) will also exit the input loop.)
        ```
    """;

    public const string Usage = $"*Usage*:\n\taxa [{ShortPlainFlag}|{LongPlainFlag}|{ShortHelpFlag}|{LongHelpFlag}] [script]";

    public enum Flags {
        None = 0,
        Help = -1,
        Plain = 2
    }

    static void Main(string[] args) {
        // parse flags
        Flags flags = Flags.None;
        while(args.Length > 0 && args[0]?[0] is '-') {
            if(args[0].Equals("-p", StringComparison.CurrentCultureIgnoreCase)
                || args[0].Equals("--plain", StringComparison.CurrentCultureIgnoreCase)
            ) {
                flags |= Flags.Plain;
            }
            else if(args[0].Equals("-h", StringComparison.CurrentCultureIgnoreCase)
                || args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase)
            ) {
                Console.WriteLine(HelpDescription);
                Environment.Exit(0);
            }
            else {
                Console.Error.WriteLine($"Invalid flag: {args[0]}. Expected {Usage}");
                Environment.Exit(1);
            }

            args = args[1..];
        }

        // parse args
        if(args.Length == 1) {
            // run the script at the file path
            Lex.FromFile(args[0], !flags.HasFlag(Flags.Plain));
        }
        else if(args.Length == 0) {
            Lex.FromLoop(!flags.HasFlag(Flags.Plain));
        }
        else {
            Console.Error.WriteLine($"Too many arguments. Expected {Usage}");
            Environment.Exit(1);
        }
    }
}

namespace Indra.Astra.CLI {

    public static class Lex {

        public static void FromFile(string path, bool colorize = true)
            => Run(new StreamReader(
                    File.OpenRead(path)
                ).AsEnumerable(), colorize);

        public static void FromLoop(bool colorize = true) {
            int line = 0;
            List<string> history = [];

            StringBuilder input = new();
            while(true) {
                // prompt line:
                int length = 0;
                string label = $"{line}> ";
                Console.Write(label);

                // read line
                while(true) {
                    // read line by character
                    ConsoleKeyInfo key = Console.ReadKey(true);

                    // esc to exit
                    if(key.Key == ConsoleKey.Escape) {
                        break;
                    }
                    else if(key.Key == ConsoleKey.Backspace) {
                        if(length == 0) {
                            Console.Write(Console.CursorLeft);
                            continue;
                        }
                        else {
                            length--;
                            input.Remove(input.Length - 1, 1);
                            Console.Write("\b \b");
                        }
                    }
                    else if(key.Key == ConsoleKey.Enter) {
                        // shift+enter to insert a new line
                        if((key.Modifiers & ConsoleModifiers.Shift) != 0) {
                            input.Append('\n');
                            line++;
                        } // just enter to run the script
                        else if(input.Length != 0) {
                            string script = input.ToString();
                            Run(script, colorize);
                            history.Add(script);
                            input.Clear();
                            line = 0;
                        }

                        Console.WriteLine();
                        break;
                    } // up arrow on empty line to repeat previous
                    else if(key.Key == ConsoleKey.UpArrow && input.Length == 0) {
                        if(history.Count > 0) {
                            Console.Write(history[^1]);
                            input.Append(history[^1]);
                        }
                    } // add the character to the input buffer
                    else {
                        input.Append(key.KeyChar);
                        length++;
                        Console.Write(key.KeyChar);
                    }
                }
            }
        }

        public static void Run(
            IEnumerable<char> script,
            bool colorize = true
        ) {
            Lexer lexer = new();
            Result result = lexer.Lex(script);
            Token[] tokens = result.Tokens ?? [];

            // top border
            Console.WriteLine();
            Console.WriteLine("============================");

            // code 
            Console.WriteLine("Code:");
            PrintCodeBlock(result, tokens, colorize);
            Console.WriteLine("---");

            // print the result status
            Console.WriteLine("Result: " + (result.IsSuccess ? "SUCCESS" : "FAILURE"));

            // print any errors
            if(result is Failure failure) {
                Console.WriteLine("---");
                Console.WriteLine("Errors:");
                foreach(Error error in failure.Errors) {
                    Console.WriteLine($"{"-",8} {error}");
                }

                Console.WriteLine();
            }

            // print the tokens
            Console.WriteLine("---");
            Console.WriteLine("Tokens:");

            if(colorize) {
                _printResultTokens_colorized(tokens, result.Source);
            }
            else {
                _printResultTokens_plain(tokens, result.Source);
            }

            // bottom border
            Console.WriteLine("============================");
            Console.WriteLine();
        }

        #region Output

        public static Dictionary<TokenType, ANSI.RGB> DefaultColors { get; }
            = new() {
                // words
                { TokenType.WORD, ANSI.RGB.Cyan },
                { TokenType.HYBRID, ANSI.RGB.Cyan.Lighter},
                { TokenType.NUMBER, ANSI.RGB.Blue },
                { TokenType.ESCAPE, ANSI.RGB.Green.Brighter },
                { TokenType.UNDERSCORE, ANSI.RGB.Cyan.Brighter },
                { TokenType.DOUBLE_UNDERSCORE, ANSI.RGB.Cyan.Brighter },
                { TokenType.TRIPLE_UNDERSCORE, ANSI.RGB.Cyan.Brighter },

                // comments
                { TokenType.CLOSE_BLOCK_COMMENT, ANSI.RGB.Gray },
                { TokenType.OPEN_BLOCK_COMMENT, ANSI.RGB.Gray },
                { TokenType.DOC_HASH_COMMENT, ANSI.RGB.Gray },
                { TokenType.EOL_HASH_COMMENT, ANSI.RGB.Gray },
                { TokenType.EOL_SLASH_COMMENT, ANSI.RGB.Gray },

                // brackets
                { TokenType.OPEN_PARENTHESIS, ANSI.RGB.Yellow },
                { TokenType.CLOSE_PARENTHESIS, ANSI.RGB.Yellow },
                { TokenType.OPEN_BRACE, ANSI.RGB.Yellow },
                { TokenType.CLOSE_BRACE, ANSI.RGB.Yellow },
                { TokenType.OPEN_BRACKET, ANSI.RGB.Yellow },
                { TokenType.CLOSE_BRACKET, ANSI.RGB.Yellow },
                { TokenType.OPEN_ANGLE, ANSI.RGB.Yellow },
                { TokenType.CLOSE_ANGLE, ANSI.RGB.Yellow },

                // quotes
                { TokenType.DOUBLE_QUOTE, ANSI.RGB.Green.Lighter },
                { TokenType.SINGLE_QUOTE, ANSI.RGB.Green.Lighter.Lighter },
                { TokenType.BACKTICK, ANSI.RGB.Yellow.Lighter },

                // assigners
                {TokenType.DOUBLE_RIGHT_ANGLE, ANSI.RGB.Red },
                {TokenType.DOUBLE_LEFT_ANGLE, ANSI.RGB.Red },
                {TokenType.EQUALS, ANSI.RGB.Red },
                {TokenType.DASH, ANSI.RGB.Red },
                {TokenType.COLON_ASSIGNER, ANSI.RGB.Red },
                {TokenType.DOUBLE_COLON_ASSIGNER, ANSI.RGB.Red },
                {TokenType.TRIPLE_COLON_ASSIGNER, ANSI.RGB.Red },
                {TokenType.RIGHT_CHEVRON, ANSI.RGB.Red },
                {TokenType.LEFT_CHEVRON, ANSI.RGB.Red },
                {TokenType.RIGHT_EQUALS_ARROW, ANSI.RGB.Red },
                {TokenType.LEFT_EQUALS_ARROW, ANSI.RGB.Red },
                {TokenType.RIGHT_TILDE_ARROW, ANSI.RGB.Red },
                {TokenType.LEFT_TILDE_ARROW, ANSI.RGB.Red },
                {TokenType.RIGHT_DASH_ARROW, ANSI.RGB.Red },
                {TokenType.LEFT_DASH_ARROW, ANSI.RGB.Red },
                {TokenType.RIGHT_PLUS_ARROW, ANSI.RGB.Red },
                {TokenType.LEFT_PLUS_ARROW, ANSI.RGB.Red },
                {TokenType.HASH_COLON, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_HASH_COLON, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_HASH_DOUBLE_COLON, ANSI.RGB.Magenta.Darker },
                {TokenType.COLON_RIGHT_ANGLE, ANSI.RGB.Magenta.Darker },
                {TokenType.COLON_DOUBLE_RIGHT_ANGLE, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_COLON_DOUBLE_RIGHT_ANGLE, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_COLON_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_COLON_RIGHT_ANGLE, ANSI.RGB.Magenta.Darker },

                // compound assigners
                {TokenType.PLUS_EQUALS, ANSI.RGB.Red.Lighter },
                {TokenType.MINUS_EQUALS, ANSI.RGB.Red.Lighter },
                {TokenType.TIMES_EQUALS, ANSI.RGB.Red.Lighter },
                {TokenType.DIVISION_EQUALS, ANSI.RGB.Red.Lighter },
                {TokenType.PERCENT_EQUALS, ANSI.RGB.Red.Lighter },
                {TokenType.DOUBLE_QUESTION_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_BANG_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.DOT_EQUALS, ANSI.RGB.Magenta.Darker },

                // comparison
                {TokenType.DOUBLE_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.GREATER_OR_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.EQUALS_OR_LESS, ANSI.RGB.Magenta.Darker },
                {TokenType.GREATER_THAN, ANSI.RGB.Magenta.Darker },
                {TokenType.LESS_THAN, ANSI.RGB.Magenta.Darker },
                {TokenType.BANG_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.QUESTION_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.HASH_EQUALS, ANSI.RGB.Magenta.Darker },
                {TokenType.DOUBLE_HASH_EQUALS, ANSI.RGB.Magenta.Darker },

                // lookup
                {TokenType.DOT, ANSI.RGB.Magenta.Brighter },
                {TokenType.SLASH, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOUBLE_DOT, ANSI.RGB.Magenta.Brighter },
                {TokenType.TRIPLE_DOT, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOUBLE_COLON_PREFIX, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOT_BANG, ANSI.RGB.Magenta.Brighter },
                {TokenType.BANG_DOT, ANSI.RGB.Magenta.Brighter },
                {TokenType.QUESTION_DOT, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOT_QUESTION, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOUBLE_DOT_BANG, ANSI.RGB.Magenta.Brighter },
                {TokenType.DOUBLE_DOT_QUESTION, ANSI.RGB.Magenta.Brighter },

                // tags
                {TokenType.HASH, ANSI.RGB.Yellow.Brighter },
                {TokenType.DOUBLE_HASH, ANSI.RGB.Yellow.Brighter },

                // tag lookups
                {TokenType.DOT_HASH, ANSI.RGB.Orange },
                {TokenType.DOUBLE_DOT_HASH, ANSI.RGB.Orange },
            };

        public static void PrintCodeBlock(Result result, Token[] tokens, bool colorize = true) {
            // top border
            Console.Write($"{"╔",8}");

            // lines of code
            Console.WriteLine(("\n"
                    + (colorize
                        ? Colorize(result.Source, tokens)
                        : result.Source)
                    ).Replace("\n", $"\n{"║",8}").PadLeft(8));

            // bottom border
            Console.WriteLine($"{"╚",8}");
        }

        public static string Colorize(Success result)
            => Colorize(result.Source, result.Tokens ?? []);

        public static string Colorize(
            string source,
            Token[] tokens
        ) {
            StringBuilder result = new();
            Stack<Token.Open> closures = [];

            int position = 0;
            foreach(Token token in tokens) {
                string padding = source[position..token.Position];
                result.Append(padding);

                string content = source[token.Position..(token.Position + token.Length)];
                position = token.End;
                ANSI.RGB color = GetColor(token);

                if(token.Type == TokenType.EOF) {
                    break;
                }
                else if(token is Token.Open start
                    && closures.TryPeek(out Token.Open? current)
                    && !current.Type.IsQuote()
                ) {
                    closures.Push(start);
                }
                else if(token is Token.Close end) {
                    if(closures.TryPeek(out Token.Open? open) && DelimiterPairs[end.Type] == open.Type) {
                        closures.Pop();
                    }
                    else {
                        color = open?.Type switch {
                            TokenType.DOUBLE_QUOTE => ANSI.RGB.Green.Darken(0.1),
                            TokenType.SINGLE_QUOTE => ANSI.RGB.Green.Lighten(0.1),
                            TokenType.BACKTICK => ANSI.RGB.Yellow.Darken(0.1),
                            _ => color
                        };
                    }
                }
                else {
                    if(closures.Count > 0) {
                        Token.Open currentDelimiter = closures.Peek();
                        color = currentDelimiter.Type switch {
                            TokenType.DOUBLE_QUOTE => ANSI.RGB.Green,
                            TokenType.SINGLE_QUOTE => ANSI.RGB.Green.Lighter,
                            TokenType.BACKTICK => ANSI.RGB.Yellow.Darker,
                            _ => color
                        };
                    }
                }

                result.Append(content.Color(color));
            }

            return result.ToString();
        }

        public static ANSI.RGB GetColor(Token token)
            => DefaultColors.TryGetValue(token.Type, out ANSI.RGB color)
                ? color
                // Operators
                : ANSI.RGB.Magenta;

        private static void _printResultTokens_colorized(
            Token[] tokens,
            string source
        ) {
            Stack<(Token.Open open, ANSI.RGB color)> closures = new Stack<(Token.Open open, ANSI.RGB color)>();
            for(int i = 0; i < tokens.Length; i++) {
                ANSI.RGB color = GetColor(tokens[i]);
                (ANSI.RGB color, int depth, bool isStart)? closure = null;

                if(tokens[i] is Token.Open open) {
                    ANSI.RGB depthColor = ANSI.RGB.Random;
                    closures.Push((open, depthColor));
                    closure = (depthColor, closures.Count, true);
                }
                else if(tokens[i] is Token.Close close) {
                    if(closures.TryPeek(out (Token.Open open, ANSI.RGB color) start)
                        && close.Open == start.open
                    ) {
                        closure = (closures.Pop().color, closures.Count + 1, false);
                    }
                }

                string text = tokens[i].ToString(source, parts => {
                    parts.name = parts.name.Color(color);

                    if (closure is not null) {
                        parts.info
                            += $"{(closure.Value.isStart ? "(" : "")}{closure.Value.depth}{(closure.Value.isStart ? ")" : "")}"
                            .Color(closure.Value.color);
                    }

                    return parts;
                });

                Console.WriteLine($"{"-",8} {tokens[i].ToString(source)}\t\t");
            }
        }

        private static void _printResultTokens_plain(
            Token[] tokens,
            string source
        ) {
            for(int i = 0; i < tokens.Length; i++) {
                Console.WriteLine($"{"-",8} {tokens[i].ToString(source)}");
            }
        }

        #endregion
    }
}