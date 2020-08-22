// ----------------------------------------------------------------------------
//  file     Program.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  31 Jul 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Emitter that generates code suitable for the 'C' family of languages
    /// (C, C++, C#, Java, etc).
    /// </summary>
    public sealed class CFamilyGenerator {
        /// <summary>
        /// Associated specification.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// The file we're outputting to.
        /// </summary>
        private readonly StreamWriter writer;

        /// <summary>
        /// Current line number.
        /// </summary>
        private int lineNum;

        /// <summary>
        /// Initializes a new instance of the <see cref="CFamilyGenerator"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="writer">File to receive output.</param>
        public CFamilyGenerator(Specification spec, StreamWriter writer) {
            this.spec = spec;
            this.writer = writer;
            lineNum = 0;
        }

        /// <summary>
        /// Generate a file containing an enum for the flags.
        /// </summary>
        public void ProcessEnum() {
            Debug.Assert(spec.HasFlags);

            ProcessCode(spec.EnumStart.RawText, 0);

            // iterate through flags
            for (int i = 0; i < spec.NumFlags; i++) {
                Flag flag = spec.GetFlag(i);

                var builder = new StringBuilder();
                builder.Append($"{flag.ToString()} = 1 << {i}");

                if (i != spec.NumFlags - 1) {
                    builder.Append(",");
                }

                EmitIndented(spec.EnumIndentation, builder.ToString());
            }

            ProcessCode(spec.EnumEnd.RawText, 0);
        }

        /// <summary>
        /// Main entry point.  Generate from the root node and onwards down the tree.
        /// </summary>
        /// <param name="node">Root node.</param>
        public void ProcessRoot(Node node) {
            ProcessCode(spec.FileStart.RawText, 0);
            ProcessNode(node, spec.RootIndentation);
            ProcessCode(spec.FileEnd.RawText, 0);
        }

        /// <summary>
        /// Send text to output stream.
        /// </summary>
        /// <param name="str">String to emit.</param>
        private void Emit(string str) {
            writer.Write(str);
        }

        /// <summary>
        /// Emit a comment.
        /// </summary>
        /// <param name="str">Contents of comment.</param>
        private void EmitComment(string str) {
            if (!spec.Config.NoPrettyOutput) {
                Emit("// ");
                Emit(str);
            }
        }

        /// <summary>
        /// Emit a new, indented line.
        /// </summary>
        /// <param name="depth">Indentation information.</param>
        /// <param name="str">String to output (optional).</param>
        private void EmitIndented(int depth, string str = "") {
            EmitNewline();

            // emit indentation
            if (!spec.Config.NoPrettyOutput) {
                var builder = new StringBuilder();
                for (int i = 0; i < depth; i++) {
                    builder.Append("    ");
                }

                Emit(builder.ToString());
            }

            // and emit the string itself
            Emit(str);
        }

        /// <summary>
        /// Emit a newline (except at start of file).
        /// </summary>
        private void EmitNewline() {
            if (lineNum > 0) {
                writer.WriteLine();
            }

            lineNum++;
        }

        /// <summary>
        /// Format a number for output where the number is considered naturally
        /// bit-oriented.
        /// </summary>
        /// <param name="bits">The value to be output.</param>
        /// <returns>String representation of the value.</returns>
        private string FormatBitwise(ulong bits) {
            return $"0x{bits:x}";
        }

        /// <summary>
        /// Generate code to extract a bitfield from the opcode. Currently we just pass through to a
        /// helper method/macro.
        /// </summary>
        /// <param name="bitfield">Specifies the desired bitfield.</param>
        /// <returns>C code to extract the bitfield.</returns>
        private string GenerateBitfield(Bitfield bitfield) {
            return $"GetBitfield(opcode, {bitfield.Start}, {bitfield.End})";
        }

        /// <summary>
        /// Generate code for a switch expression derived from multiple bitfields.
        /// </summary>
        /// <param name="bitfieldSet">Bitfield set object.</param>
        /// <returns>
        /// A string with 'C' code that extracts the bitfields from the instruction word and
        /// recombines them in the desired form.
        /// </returns>
        private string GenerateBitfieldSet(BitfieldSet bitfieldSet) {
            var builder = new StringBuilder();

            // iterate through bitfields
            int totalBits = 0;
            for (int i = 0; i < bitfieldSet.Bitfields.Count; i++) {
                Bitfield bf = bitfieldSet.Bitfields[i];

                // separate bitfields with bitwise OR
                if (i != 0) {
                    builder.Append(" | ");
                }

                // wrap shifted bitfields in parentheses
                if (totalBits != 0) {
                    builder.Append($"({GenerateBitfield(bf)} << {totalBits})");
                } else {
                    builder.Append(GenerateBitfield(bf));
                }

                totalBits += bf.NumBits;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Process the bitwise component of a condition.
        /// </summary>
        /// <param name="cond">The condition to be processed.</param>
        /// <returns>Equivalent C code in string form.</returns>
        private string ProcessBitwiseCondition(Condition cond) {
            if (cond.DecodeBits.IsEmpty) {
                return "";
            }

            // TODO: this needs to be extended to handle decoding > 64 bits
            return string.Format("(opcode & {0}) == {1}",
                FormatBitwise(cond.DecodeBits.GetMaskUlong(0)),
                FormatBitwise(cond.DecodeBits.GetValueUlong(0)));
        }

        /// <summary>
        /// Process a string of code, and output it verbatim (except with indentation).
        /// </summary>
        /// <param name="fragment">Code fragment, lines separated by '\n'.</param>
        /// <param name="info">Current indentation settings.</param>
        private void ProcessCode(string fragment, int depth) {
            string[] lines = fragment.Split('\n');

            foreach (string line in lines) {
                // indentation will be wrong for code fragments containing braces, but tracking them
                // correctly will introduce huge complexity, and for very little gain.
                EmitIndented(depth, line);
            }
        }

        /// <summary>
        /// Process a condition and emit equivalent C code.
        /// </summary>
        /// <param name="cond">The condition to be processed.</param>
        private void ProcessCondition(Condition cond) {
            string bStr = ProcessBitwiseCondition(cond);
            string fStr = ProcessFlagCondition(cond);

            if (bStr.Length != 0) {
                if (fStr.Length != 0) {
                    // emit both conditions
                    Emit($"{bStr} && {fStr}");
                } else {
                    // emit just the bitwise condition
                    Emit(bStr);
                }
            } else {
                // emit just the flags condition
                Debug.Assert(fStr.Length != 0);
                Emit(fStr);
            }
        }

        /// <summary>
        /// Process the flags component of a condition.
        /// </summary>
        /// <param name="cond">Condition to be processed.</param>
        /// <returns>String containing C code for an expression that tests for the desired condition.</returns>
        private string ProcessFlagCondition(Condition cond) {
            if (cond.Flags.IsEmpty) {
                return "";
            }

            // TODO: this needs to be extended to handle > 64 flags
            return string.Format("({0} & {1}) == {2}",
                spec.DecodeFlags.RawText,
                FormatBitwise(cond.Flags.GetMaskUlong(0)),
                FormatBitwise(cond.Flags.GetValueUlong(0)));
        }

        /// <summary>
        /// Process an If-Else node.
        /// </summary>
        /// <param name="node">If-Else node to be processed.</param>
        /// <param name="depth">Current indentation.</param>
        /// <param name="omitIndent">If true, the 'if' should NOT be indented.</param>
        private void ProcessIfElse(IfElseNode node, int depth, bool omitIndent) {
            if (omitIndent) {
                Emit("if (");
            } else {
                EmitIndented(depth, "if (");
            }

            ProcessCondition(node.Condition);

            Emit(") {");

            ProcessNode(node.IfBranch, depth + 1);

            if (node.ElseBranch is EmptyNode) {
                EmitIndented(depth, "}");
            } else if (node.ElseBranch is IfElseNode) {
                EmitIndented(depth, "} else ");
                ProcessIfElse((IfElseNode) node.ElseBranch, depth, true);

                // no close brace!!!
            } else {
                EmitIndented(depth, "} else {");
                ProcessNode(node.ElseBranch, depth + 1);
                EmitIndented(depth, "}");
            }
        }

        /// <summary>
        /// Process a node in the tree, generating code appropriate for its type.
        /// </summary>
        /// <param name="node">Node to be processed.</param>
        /// <param name="depth">Current indentation settings.</param>
        private void ProcessNode(Node node, int depth) {
            if (node is Rule rNode) {
                ProcessRule(rNode, depth);
            } else if (node is SequenceNode sqNode) {
                ProcessSequence(sqNode, depth);
            } else if (node is IfElseNode ifNode) {
                ProcessIfElse(ifNode, depth, false);
            } else if (node is SwitchNode swNode) {
                ProcessSwitch(swNode, depth);
            }
        }

        /// <summary>
        /// Process a Rule node.
        /// </summary>
        private void ProcessRule(Rule node, int depth) {
            // start a new indented line
            EmitIndented(depth);

            if (!spec.Config.NoPrettyOutput) {
                // build a comment for the rule
                var strb = new StringBuilder();
                strb.Append($"rule at line {node.LineNum}");

                // append the rule's condition
                string condComment = node.Condition.GetFriendlyString();
                if (condComment.Length != 0) {
                    strb.Append(" : ");
                    strb.Append(condComment.ToString());
                }

                // output the comment
                EmitComment(strb.ToString());
            }

            // output the code fragment
            ProcessCode(node.CodeFragment.RawText, depth);

            // insert a 'return' statement, if needed
            if (spec.Config.InsertReturns) {
                EmitIndented(depth, "return;");
            }
        }

        /// <summary>
        /// Process a sequence of nodes.
        /// </summary>
        private void ProcessSequence(SequenceNode node, int depth) {
            // just process each node in the sequence
            for (int i = 0; i < node.Length; i++) {
                ProcessNode(node[i], depth);
            }
        }

        /// <summary>
        /// Process a SwitchNode and generate equivalent C code.
        /// </summary>
        private void ProcessSwitch(SwitchNode node, int depth) {
            if (node.Expression is Bitfield) {
                var bf = (Bitfield) node.Expression;
                EmitIndented(depth, $"switch ({GenerateBitfield(bf)}) {{");
            } else {
                Debug.Assert(node.Expression is BitfieldSet);
                var bfSet = (BitfieldSet) node.Expression;
                EmitIndented(depth, $"switch ({GenerateBitfieldSet(bfSet)}) {{");
            }

            ProcessSwitchChildren(depth, node);

            EmitIndented(depth, "}");
        }

        /// <summary>
        /// Process the child nodes of a SwitchNode, and generate 'case's for them.
        /// </summary>
        private void ProcessSwitchChildren(int depth, SwitchNode node) {
            for (int i = 0; i < node.Expression.NumValues; i++) {
                // skip over child references in the outer loop.  once we get to a real
                // child node, we'll scan for ChildReferenceNodes.
                if (!(node[i] is ChildReferenceNode)) {
                    // search for child references and generate fall thru cases
                    for (int j = i + 1; j < node.Expression.NumValues; j++) {
                        if (node[j] is ChildReferenceNode &&
                            ((ChildReferenceNode) node[j]).Index == i) {
                            EmitIndented(depth, $"case 0x{j:x}: ");
                            EmitComment("fall thru");
                        }
                    }

                    // emit case label
                    EmitIndented(depth, $"case 0x{i:x}:");

                    // emit children, followed by 'break'
                    ProcessNode(node[i], depth + 1);

                    if (!(node[i] is Rule) || !spec.Config.NoBreakAfterRule) {
                        EmitIndented(depth + 1, "break;");
                    }
                }
            }
        }

        // current 0-based line number of output
    }
}