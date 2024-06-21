using System.Text;

using Indra.Astra.CLI;
using Indra.Astra.Tokens;

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

        public static Dictionary<IToken, ANSI.RGB> DefaultColors { get; }
            = new() {
                // words
                {Word.Type, ANSI.RGB.Cyan },
                {Number.Type, ANSI.RGB.Blue },
                {Escape.Type, ANSI.RGB.Green.Brighter },

                // comments
                {CloseBlockComment.Type, ANSI.RGB.Gray },
                {OpenBlockComment.Type, ANSI.RGB.Gray },

                // brackets
                {LeftParenthesis.Type, ANSI.RGB.Yellow },
                {RightParenthesis.Type, ANSI.RGB.Yellow },
                {LeftBrace.Type, ANSI.RGB.Yellow },
                {RightBrace.Type, ANSI.RGB.Yellow },
                {LeftBracket.Type, ANSI.RGB.Yellow },
                {RightBracket.Type, ANSI.RGB.Yellow },
                {LeftAngle.Type, ANSI.RGB.Yellow },
                {RightAngle.Type, ANSI.RGB.Yellow },

                // quotes
                {DoubleQuote.Type, ANSI.RGB.Green.Lighter },
                {SingleQuote.Type, ANSI.RGB.Green.Lighter.Lighter },
                {Backtick.Type, ANSI.RGB.Yellow.Lighter },

                // assigners
                {DoubleRightAngle.Type, ANSI.RGB.Red },
                {DoubleLeftAngle.Type, ANSI.RGB.Red },
                {Equal.Type, ANSI.RGB.Red },
                {Dash.Type, ANSI.RGB.Red },
                {DoubleColon.Type, ANSI.RGB.Red },
                {TripleColon.Type, ANSI.RGB.Red },
                {TripleLeftAngle.Type, ANSI.RGB.Red },
                {TripleRightAngle.Type, ANSI.RGB.Red },
                {RightEqualArrow.Type, ANSI.RGB.Red },
                {LeftEqualArrow.Type, ANSI.RGB.Red },
                {RightTildeArrow.Type, ANSI.RGB.Red },
                {LeftTildeArrow.Type, ANSI.RGB.Red },
                {RightDashArrow.Type, ANSI.RGB.Red },
                {LeftDashArrow.Type, ANSI.RGB.Red },
                {RightPlusArrow.Type, ANSI.RGB.Red },
                {LeftPlusArrow.Type, ANSI.RGB.Red },

                // compound assigners
                {PlusEqual.Type, ANSI.RGB.Red.Lighter },
                {DashEqual.Type, ANSI.RGB.Red.Lighter },
                {StarEqual.Type, ANSI.RGB.Red.Lighter },
                {SlashEqual.Type, ANSI.RGB.Red.Lighter },
                {PercentEqual.Type, ANSI.RGB.Red.Lighter },
                {DoubleQuestionEqual.Type, ANSI.RGB.Magenta.Darker },
                {DoubleBangEqual.Type, ANSI.RGB.Magenta.Darker },
                {DotEqual.Type, ANSI.RGB.Magenta.Darker },

                // comparison
                {DoubleEqual.Type, ANSI.RGB.Magenta.Darker },
                {BangEqual.Type, ANSI.RGB.Magenta.Darker },
                {QuestionEqual.Type, ANSI.RGB.Magenta.Darker },
                {HashEqual.Type, ANSI.RGB.Magenta.Darker },
                {DoubleHashEqual.Type, ANSI.RGB.Magenta.Darker },

                // lookup
                {Dot.Type, ANSI.RGB.Magenta.Brighter },
                {Slash.Type, ANSI.RGB.Magenta.Brighter },
                {DoubleDot.Type, ANSI.RGB.Magenta.Brighter },
                {TripleDot.Type, ANSI.RGB.Magenta.Brighter },

                // tags
                {Hash.Type, ANSI.RGB.Yellow.Brighter },
                {DoubleHash.Type, ANSI.RGB.Yellow.Brighter },
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

            int position = 0;
            foreach(Token token in tokens) {
                if(token.Type is EndOfFile) {
                    break;
                }

                string padding = source[position..token.Index];
                result.Append(padding);

                string content = source[token.Index..(token.Index + token.Length)];
                position = token.End;
                ANSI.RGB color = GetColor(token);

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
            for(int i = 0; i < tokens.Length; i++) {
                ANSI.RGB color = GetColor(tokens[i]);

                string text = tokens[i].ToString(source, parts => {
                    parts.name = parts.name.Color(color);
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