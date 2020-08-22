// ----------------------------------------------------------------------------
//  file     SpecLexer.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  12 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System;
    using System.Data;
    using System.IO;

    /// <summary>
    /// Represents an indivisible lexical unit of source text in the specification -
    /// a 'word' in the specification's grammar.
    /// </summary>
    internal struct Token {
        /// <summary>
        /// String value.
        /// </summary>
        private readonly string strValue;

        /// <summary>
        /// Type of token.
        /// </summary>
        private readonly Type type;

        /// <summary>
        /// Unsigned integral value.
        /// </summary>
        private readonly ulong uValue;

        /// <summary>
        /// Floating point value.
        /// </summary>
        private readonly float fValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> struct.
        /// </summary>
        public Token(Type type) {
            this.type = type;
            strValue = "";
            uValue = 0;
            fValue = 0F;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> struct with a string parameter.
        /// </summary>
        public Token(Type type, string str) {
            this.type = type;
            strValue = str;
            uValue = 0;
            fValue = 0F;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> struct with an integer parameter.
        /// </summary>
        public Token(Type type, ulong uValue) {
            this.type = type;
            strValue = "";
            this.uValue = uValue;
            this.fValue = 0F;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> struct with a floating point parameter.
        /// </summary>
        public Token(Type type, float fValue) {
            this.type = type;
            strValue = "";
            this.uValue = 0;
            this.fValue = fValue;
        }

        /// <summary>
        /// Token type code.
        /// </summary>
        public enum Type {
            Eof,
            CodeFragment,
            Directive,
            BeginPattern,
            EndPattern,
            BeginFlags,
            EndFlags,
            Identifier,
            Integer,
            Float,
            Not,
            Comma,
            Dollar,
            One,
            Zero,
            Dot
        }

        /// <summary>
        /// Gets type code.
        /// </summary>
        public Type Code => type;

        /// <summary>
        /// Gets string value.
        /// </summary>
        public string String => strValue;

        /// <summary>
        /// Gets ulong value.
        /// </summary>
        public ulong Ulong => uValue;

        /// <summary>
        /// Convert to a string for diagnostics.
        /// </summary>
        public override string ToString() {
            switch (type) {
            case Type.CodeFragment:
            case Type.Identifier:
                return $"{type.ToString().ToLower()} \"{strValue}\"";

            case Type.Integer:
                return $"{type.ToString().ToLower()} #{uValue}";

            case Type.Float:
                return $"{type.ToString().ToLower()} #{fValue}";

            default:
                return type.ToString();
            }
        }
    }

    /// <summary>
    /// Lexical analyser for decoder specifications.  Responsible for retrieving the input
    /// and breaking it into a series of lexical tokens.
    /// </summary>
    internal class SpecLexer {
        /// <summary>
        /// Associated file (if there is one).
        /// </summary>
        private readonly StreamReader? file;

        /// <summary>
        /// Current line (with '\n' sentinel).
        /// </summary>
        private string line;

        /// <summary>
        /// Current 1-based line number.
        /// </summary>
        private int lineNum;

        /// <summary>
        /// Next character - 0-based index into line.
        /// </summary>
        private int offset;

        /// <summary>
        /// Current state.
        /// </summary>
        private State state;

        /// <summary>
        /// Current token.
        /// </summary>
        private Token token;

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecLexer"/> class. Constructor for
        /// processing a file or resource.
        /// </summary>
        public SpecLexer(StreamReader file) {
            this.file = file;
            lineNum = 0;
            line = "\n";
            offset = 0;
            state = State.StartOfLine;

            Next();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpecLexer"/> class. Constructor for
        /// processing a single-line string.
        /// </summary>
        public SpecLexer(string line) {
            file = null;

            lineNum = 0;
            this.line = line + "\n";
            offset = 0;
            state = State.Normal;

            Next();
        }

        /// <summary>
        /// Internal state of lexical analyser.
        /// </summary>
        private enum State {
            StartOfLine,    // at or before the first token in a line
            Normal,         // reading normal tokens
            Pattern,        // reading bit pattern tokens
            Eof             // at end of input
        }

        /// <summary>
        /// Gets current token.
        /// </summary>
        public Token Crnt => token;

        /// <summary>
        /// Gets current line number.
        /// </summary>
        public int LineNum => lineNum;

        /// <summary>
        /// Fetch another token from the input.
        /// </summary>
        public void Next() {
            SkipWhitespace();

            // decide what to do based on the current state
            char crntChar = line[offset];
            switch (state) {
            case State.StartOfLine:
                // did SkipWhitespace move us past the first column?
                if (offset == 0) {
                    switch (crntChar) {
                    case '%':
                        // %-prefixed lines are directives
                        offset++;
                        state = State.Normal;
                        token = new Token(Token.Type.Directive);
                        break;
                    // @-prefixed lines are whitespace-preserving code fragments
                    case '@':
                        ConsumeLine();
                        token = new Token(Token.Type.CodeFragment, line.Substring(1, line.Length - 2));
                        break;
                    // otherwise, treat it as a bit pattern
                    default:
                        state = State.Pattern;
                        token = new Token(Token.Type.BeginPattern);
                        break;
                    }
                } else {
                    // whitespace-prefixed lines (ie., indented lines) are
                    // whitespace-stripping code fragments
                    ConsumeLine();
                    token = new Token(Token.Type.CodeFragment, line.Trim());
                }

                break;

            case State.Normal:
                GetNormal(crntChar);
                break;

            case State.Pattern:
                GetPattern(crntChar);
                break;

            case State.Eof:
                break;
            }
        }

        /// <summary>
        /// Consume the remainder of the line.
        /// </summary>
        private void ConsumeLine() {
            offset = line.Length - 1;
        }

        /// <summary>
        /// Scan an identifier. As per C, etc, identifiers match the
        /// regex: /[A-Za-z_][A-Za-z0-9_].*/
        /// </summary>
        /// <param name="crntChar">First character of identifier.</param>
        /// <returns>True if an identifier was successfully processed.</returns>
        private bool GetIdentifier(char crntChar) {
            if (!(char.IsLetter(crntChar) || crntChar == '_')) {
                return false;
            }

            // find first character after identifier
            int start = offset;
            do {
                offset++;
                crntChar = line[offset];
            } while (char.IsLetterOrDigit(crntChar) || crntChar == '_');

            // initialise token
            token = new Token(Token.Type.Identifier, line.Substring(start, offset - start));
            return true;
        }

        /// <summary>
        /// Fetch a line of input text.
        /// </summary>
        /// <returns>True if successful, or false at end-of-input.</returns>
        private bool GetLine() {
            if (file == null || file.EndOfStream) {
                state = State.Eof;
                token = new Token(Token.Type.Eof);
                return false;
            }

            line = file.ReadLine() + '\n';
            lineNum++;
            offset = 0;
            state = State.StartOfLine;
            return true;
        }

        /// <summary>
        /// Scan a token in the 'normal' state (used when parsing directives and in flags specifications).
        /// </summary>
        /// <param name="crntChar">Initial character of token.</param>
        private void GetNormal(char crntChar) {
            switch (crntChar) {
            case '[':
                offset++;
                token = new Token(Token.Type.BeginFlags);
                return;

            case ']':
                offset++;
                token = new Token(Token.Type.EndFlags);
                return;

            case ':':
                offset++;
                token = new Token(Token.Type.CodeFragment, line.Substring(offset).Trim());
                ConsumeLine();
                return;

            case '!':
                offset++;
                token = new Token(Token.Type.Not);
                return;

            case ',':
                offset++;
                token = new Token(Token.Type.Comma);
                return;

            case '$':
                offset++;
                token = new Token(Token.Type.Dollar);
                return;

            default:
                if (GetIdentifier(crntChar)) {
                    return;
                } else if (GetNumber(crntChar)) {
                    return;
                } else {
                    throw new SyntaxErrorException($"Mistake at line {lineNum}");
                }
            }
        }

        /// <summary>
        /// Scan a numeric token.  Currently the only supported number formats are
        /// floating point and integers, both in decimal.
        /// </summary>
        /// <param name="crntChar">First character of token.</param>
        /// <returns>True if a syntactically valid number was processed.</returns>
        private bool GetNumber(char crntChar) {
            if (!char.IsDigit(crntChar)) {
                return false;
            }

            // determine length of string of digits
            int start = offset;

            // a number is either [digit]+ or [digit]+.[digit]+
            bool hasDot = false;
            while (true) {
                do {
                    offset++;
                    crntChar = line[offset];
                } while (char.IsDigit(crntChar));

                if (!hasDot && crntChar == '.') {
                    hasDot = true;
                } else {
                    break;
                }
            }

            try {
                if (!hasDot) {
                    // parse the actual number & handle errors
                    ulong value;
                    value = Convert.ToUInt64(line.Substring(start, offset - start));
                    token = new Token(Token.Type.Integer, value);
                } else {
                    float value;
                    value = (float) Convert.ToDouble(line.Substring(start, offset - start));
                    token = new Token(Token.Type.Float, value);
                }
            } catch (Exception) {
                throw new SyntaxErrorException($"Bad numeric value at line {lineNum}");
            }

            return true;
        }

        /// <summary>
        /// Scan a token in the 'pattern' state (used when parsing bit patterns
        /// in rules).
        /// </summary>
        /// <param name="crntChar">Initial character of token.</param>
        private void GetPattern(char crntChar) {
            switch (crntChar) {
            case '0':
                offset++;
                token = new Token(Token.Type.Zero);
                return;

            case '1':
                offset++;
                token = new Token(Token.Type.One);
                return;

            case '.':
                offset++;
                token = new Token(Token.Type.Dot);
                return;

            default:
                // generate an EndPattern token and switch to the 'normal'
                // state in order to process the character
                state = State.Normal;
                token = new Token(Token.Type.EndPattern);
                return;
            }
        }

        /// <summary>
        /// Advance to the next non-whitespace token, fetching new lines as required.
        /// </summary>
        private void SkipWhitespace() {
            if (state == State.Eof) {
                return;
            }

            do {
                // skip characters until we find something that isn't whitespace
                char crntChar = line[offset];
                while (char.IsWhiteSpace(crntChar) && crntChar != '\n') {
                    offset++;
                    crntChar = line[offset];
                }

                // do we need to fetch another line?
                switch (crntChar) {
                case '\n':
                case '#':
                    if (!GetLine()) {
                        return;
                    }

                    break;

                default:
                    return;
                }
            } while (true);
        }
    }
}