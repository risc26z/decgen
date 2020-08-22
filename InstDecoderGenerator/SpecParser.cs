// ----------------------------------------------------------------------------
//  file     SpecParser.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  13 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.Data;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// The SpecParser is responsible for parsing a decoder specification and
    /// initialising a Specification object with its contents.  There are
    /// two public entry points: Parse() and ParseFlags().  Both of these are
    /// static.
    /// </summary>
    public class SpecParser {
        /// <summary>
        /// Associated lexical analyser.
        /// </summary>
        private readonly SpecLexer lexer;

        /// <summary>
        /// Associated specification.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecParser"/> class.
        /// </summary>
        /// <param name="spec">Associated Specification object.</param>
        /// <param name="lexer">Associated SpecLexer, responsible for breaking the
        /// input into a sequence of Tokens and providing these to the parser.</param>
        private SpecParser(Specification spec, SpecLexer lexer) {
            this.lexer = lexer;
            this.spec = spec;
        }

        /// <summary>
        /// Gets the type code of the lexer's current token.
        /// </summary>
        private Token.Type CrntCode => lexer.Crnt.Code;

        /// <summary>
        /// Parse a specification file/resource and populate the Specification
        /// with its contents.
        /// </summary>
        public static void Parse(Specification spec, StreamReader file) {
            var lexer = new SpecLexer(file);
            var parser = new SpecParser(spec, lexer);
            parser.Parse();
        }

        /// <summary>
        /// Parse a string containing a flags specification, using the
        /// supplied Specification to look up flag names, and return the
        /// flag specification in binary form.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="flags">String to be parsed.  The format is the
        /// same as that in specification rules (excluding the square
        /// brackets).</param>
        /// <returns>Binary representation of the specified flags.</returns>
        public static TristateBitArray ParseFlags(Specification spec, string flags) {
            var lexer = new SpecLexer(flags);
            var parser = new SpecParser(spec, lexer);
            return parser.ParseFlags();
        }

        /// <summary>
        /// Verifies that the current token is of the expected type, and generates
        /// an error if not.
        /// </summary>
        /// <param name="type"></param>
        private void Expect(Token.Type type) {
            if (CrntCode != type) {
                SyntaxError($"Unexpected {lexer.Crnt}; expected {type}");
            }
        }

        /// <summary>
        /// Advance to the next token.
        /// </summary>
        private void Next() {
            lexer.Next();
        }

        /// <summary>
        /// Simple wrapper for a Next(), Expect() sequence.
        /// </summary>
        private void NextExpect(Token.Type type) {
            Next();
            Expect(type);
        }

        /// <summary>
        /// Top-level parsing method.  The top level is a sequence of X's, followed by
        /// and EOF.  Currently the only X's allowed are directives and patterns.
        /// </summary>
        private void Parse() {
            while (CrntCode != Token.Type.Eof) {
                switch (CrntCode) {
                case Token.Type.Directive:
                    ParseDirective();
                    break;

                case Token.Type.BeginPattern:
                    ParseRule();
                    break;

                default:
                    SyntaxError($"Unexpected {lexer.Crnt}");
                    break;
                }
            }
        }

        /// <summary>
        /// Parse a directive.  This consists of the '%' character at the start of a
        /// line, followed by an identifier (specifying the directive).  This may be
        /// followed by directive-specific arguments.
        /// </summary>
        private void ParseDirective() {
            if (spec.Rules.Any()) {
                SyntaxError("Directives must appear before rules");
            }

            // dispatch according to the literal text of the identifier
            NextExpect(Token.Type.Identifier);
            switch (lexer.Crnt.String) {
            // %fileStart FRAGMENT+
            case "fileStart":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.FileStart);
                break;
            // %fileEnd FRAGMENT+
            case "fileEnd":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.FileEnd);
                break;
            // %enumStart FRAGMENT+
            case "enumStart":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.EnumStart);
                break;
            // %enumEnd FRAGMENT+
            case "enumEnd":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.EnumEnd);
                break;
            // %decodeFlags FRAGMENT+
            case "decodeFlags":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.DecodeFlags);
                break;
            // %decodeFlags FRAGMENT+
            case "fetch":
                NextExpect(Token.Type.CodeFragment);
                ParseFragments(spec.FetchFrag);
                break;
            // %bits INTEGER
            case "bits":
                NextExpect(Token.Type.Integer);
                if (lexer.Crnt.Ulong == 0) {
                    SyntaxError("Nonsensical 'bits' directive");
                }

                spec.NumBits = (int) lexer.Crnt.Ulong;
                Next();
                break;
            // %rootIndentation INTEGER
            case "rootIndentation":
                NextExpect(Token.Type.Integer);
                spec.RootIndentation = (int) lexer.Crnt.Ulong;
                Next();
                break;
            // %enumIndentation INTEGER
            case "enumIndentation":
                NextExpect(Token.Type.Integer);
                spec.EnumIndentation = (int) lexer.Crnt.Ulong;
                Next();
                break;

            case "flag":
                // %flag IDENTIFIER
                NextExpect(Token.Type.Identifier);
                if (spec.GetFlag(lexer.Crnt.String) != null) {
                    SyntaxError($"Flag {lexer.Crnt.String} already declared");
                }

                spec.AddFlag(lexer.Crnt.String);
                Next();
                break;

            default:
                SyntaxError("Unknown directive");
                break;
            }
        }

        /// <summary>
        /// Parse a flag specification: a comma-separated list of flag identifiers, each
        /// preceded by an optional '!' token.  A flag specification is allowed to be
        /// empty.
        /// </summary>
        /// <returns>Equivalent bit pattern for flags.</returns>
        private TristateBitArray ParseFlags() {
            var bits = new TristateBitArray(spec.NumFlags);
            while (CrntCode == Token.Type.Not || CrntCode == Token.Type.Identifier) {
                // parse optional '!'
                bool invert = false;
                if (CrntCode == Token.Type.Not) {
                    invert = true;
                    Next();
                }

                // now parse identifier
                Expect(Token.Type.Identifier);
                Flag? flag = spec.GetFlag(lexer.Crnt.String);
                if (flag == null) {
                    SyntaxError($"Undeclared flag {lexer.Crnt.String}");
                } else {
                    bits.SetBit(flag.Index, invert ? TristateBitArray.BitValue.Zero : TristateBitArray.BitValue.One);
                }

                // is the following token a comma?  if so, we can loop around to the
                // next flag
                Next();
                if (CrntCode != Token.Type.Comma) {
                    break;
                }

                // skip over comma
                Next();
            }

            return bits;
        }

        /// <summary>
        /// Parse a sequence of zero or more code fragments.
        /// </summary>
        /// <param name="fragment">CodeFragment object to which fragment text
        /// should be appended.</param>
        private void ParseFragments(CodeFragment fragment) {
            while (CrntCode == Token.Type.CodeFragment) {
                if (fragment.RawText.Length != 0) {
                    fragment.Append("\n");
                }

                fragment.Append(lexer.Crnt.String);
                Next();
            }
        }

        /// <summary>
        /// Parse the 'bits' part of a pattern specification. This consists of a sequence of '0',
        /// '1', or '.' bits, and is terminated by an EndPattern token (which doesn't directly map
        /// to an input character, but is generated by the lexer when it finds a non-bit character.
        /// </summary>
        /// <returns>Equivalent bit array.</returns>
        private TristateBitArray ParsePattern() {
            int i = spec.NumBits - 1;
            var value = new TristateBitArray(spec.NumBits);

            // loop over input tokens until we reach the terminator
            while (CrntCode != Token.Type.EndPattern) {
                // map the input token to a bit value
                TristateBitArray.BitValue bitValue = CrntCode switch
                {
                    Token.Type.Zero => TristateBitArray.BitValue.Zero,
                    Token.Type.One => TristateBitArray.BitValue.One,
                    Token.Type.Dot => TristateBitArray.BitValue.Undefined,
                    _ => throw new SyntaxErrorException(SyntaxErrorMessage(UnexpectedErrorMessage()))
                };

                // abort if we've received too many bits
                if (i < 0) {
                    break;
                }

                // set the bit
                value.SetBit(i, bitValue);
                i--;

                Next();
            }

            // verify that we parsed the correct number of bits
            if (i != -1) {
                SyntaxError("Incorrect bit count in pattern");
            }

            // skip 'endpattern' token
            Next();

            return value;
        }

        /// <summary>
        /// Parse a pattern rule.  This consists of:
        ///   a bit pattern (mandatory)
        ///   a weight (optional)
        ///   a flags specification (optional)
        ///   one or more code fragments.
        /// </summary>
        private void ParseRule() {
            int lineNum = lexer.LineNum;
            Next();

            if (spec.NumBits == 0) {
                SyntaxError("Missing 'bits' directive");
            }

            // parse the pattern
            TristateBitArray bits = ParsePattern();

            var flags = new TristateBitArray(spec.NumFlags);
            var fragment = new CodeFragment(spec);
            ulong weight = 1;

            // parse optional weight
            if (CrntCode == Token.Type.Dollar) {
                NextExpect(Token.Type.Float);
                weight = lexer.Crnt.Ulong;
                Next();
            }

            // parse optional flags (between '[' and ']')
            if (CrntCode == Token.Type.BeginFlags) {
                Next();
                flags = ParseFlags();
                Expect(Token.Type.EndFlags);
                Next();
            }

            // parse 1+ code fragments
            Expect(Token.Type.CodeFragment);
            ParseFragments(fragment);

            // generate rule and add it to the Specification
            var condition = new Condition(spec, bits, flags);
            var rule = new Rule(spec, condition, fragment, (int) weight, lineNum);
            spec.AddRule(rule);
        }

        /// <summary>
        /// Generate an exception for a syntax error in the input.
        /// </summary>
        /// <param name="message">Message.</param>
        private void SyntaxError(string message) {
            throw new SyntaxErrorException(SyntaxErrorMessage(message));
        }

        /// <summary>
        /// Return the string representation of a syntax error message.
        /// </summary>
        /// <param name="message">Specific error.</param>
        /// <returns></returns>
        private string SyntaxErrorMessage(string message) {
            return $"Syntax error: {message} at line {lexer.LineNum}";
        }

        /// <summary>
        /// Return an error message for an unexpected token.
        /// </summary>
        /// <returns></returns>
        private string UnexpectedErrorMessage() {
            return $"Unexpected {lexer.Crnt}";
        }
    }
}