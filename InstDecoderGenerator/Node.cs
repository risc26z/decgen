// ----------------------------------------------------------------------------
//  file     Node.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  31 Jul 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.Collections.Generic;
    using System.Text;

    /// <summary> Only found as a child of a <see cref="SwitchNode"/. >A "pointer" to another child
    /// of the same parent, which must be a SwitchNode. Allows code sharing via fall-thru cases.</summary>
    public class ChildReferenceNode : Node {
        /// <summary>
        /// Case value of target node.
        /// </summary>
        private readonly int index;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChildReferenceNode"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="index">Equivalent case value.</param>
        public ChildReferenceNode(Specification spec, int index)
            : base(spec) => this.index = index;

        /// <summary>
        /// Gets the equivalent case value.
        /// </summary>
        public int Index => index;
    }

    /// <summary>
    /// A node containing arbitrary code.
    /// </summary>
    public class CodeFragment : Node {
        /// <summary>
        /// String holding unprocessed text of the code fragment.
        /// </summary>
        private readonly StringBuilder fragment;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeFragment"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        public CodeFragment(Specification spec)
            : base(spec) => fragment = new StringBuilder();

        /// <summary>
        /// Gets the unprocessed code in textual form.
        /// </summary>
        public string RawText => fragment.ToString();

        /// <summary>
        /// Append text to the code fragment.
        /// </summary>
        /// <param name="str">String to be appended.</param>
        public void Append(string str) {
            fragment.Append(str);
        }

        /// <summary>
        /// Gets the code in a form suitable for diagnostics.
        /// </summary>
        public override string ToString() {
            return fragment.ToString().Replace("\n", "\\n");
        }
    }

    /// <summary>
    /// A trivial <seealso cref="Node"/> that does nothing.
    /// </summary>
    public class EmptyNode : Node {
        /// <summary>
        /// Initializes a new instance of the <see cref="EmptyNode"/> class.
        /// </summary>
        /// <param name="spec"></param>
        public EmptyNode(Specification spec)
            : base(spec) {
        }

        /// <summary>
        /// Test equality.  All empty nodes are considered equal.
        /// </summary>
        public override bool Equals(object? obj) {
            return obj is EmptyNode;
        }

        /// <summary>
        /// Trivial GetHashCode implementation.  Needs proper implementation.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return base.GetHashCode();
        }
    }

    /// <summary>
    /// Decoder tree representation of an 'if' statement, complete with 'else' branch.
    /// </summary>
    public class IfElseNode : Node {
        private readonly Condition condition;
        private readonly Node elseBranch;
        private readonly Node ifBranch;

        /// <summary>
        /// Initializes a new instance of the <see cref="IfElseNode"/> class.
        /// </summary>
        public IfElseNode(Specification spec,
                          Condition condition,
                          Node ifBranch,
                          Node elseBranch)
            : base(spec) {
            this.condition = condition;
            this.ifBranch = ifBranch;
            this.elseBranch = elseBranch;
        }

        /// <summary>
        /// Gets the condition for the 'if' branch.
        /// </summary>
        public Condition Condition => condition;

        /// <summary>
        /// Gets the 'else' branch.
        /// </summary>
        public Node ElseBranch => elseBranch;

        /// <summary>
        /// Gets the 'if' branch.
        /// </summary>
        public Node IfBranch => ifBranch;

        /// <summary>
        /// Test for equality.  If-else nodes are equal if the condition and both branches are equal.
        /// </summary>
        public override bool Equals(object? n) {
            if (n is IfElseNode node) {
                return condition.Equals(node.condition) &&
                    ifBranch.Equals(node.ifBranch) &&
                    elseBranch.Equals(node.elseBranch);
            } else {
                return false;
            }
        }

        /// <summary>
        /// Trivial GetHashCode implementation.  Needs proper implementation.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return base.GetHashCode();
        }

        /// <summary>
        /// Touch the node, the condition, and both branches.
        /// </summary>
        public override void Touch(TouchFunction touchFunc) {
            touchFunc(this);
            condition.Touch(touchFunc);
            ifBranch.Touch(touchFunc);
            elseBranch.Touch(touchFunc);
        }
    }

    /// <summary>
    /// The ultimate base type for all objects in the internal tree
    /// representation of the generated decoder.
    /// </summary>
    public class Node {
        /// <summary>
        /// Associated Specification object.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// Initializes a new instance of the <see cref="Node"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        protected Node(Specification spec) => this.spec = spec;

        /// <summary>
        /// See <see cref="Touch"/>.
        /// </summary>
        public delegate void TouchFunction(Node node);

        /// <summary>
        /// Gets accessor for associated <seealso cref="Specification"/>.
        /// </summary>
        public Specification Spec => spec;

        /// <summary>
        /// Invoke a function, passing as a parameter this node and all of
        /// its descendants.
        /// </summary>
        /// <param name="touchFunc">Function to be called.</param>
        public virtual void Touch(TouchFunction touchFunc) {
            touchFunc(this);
        }
    }

    public class SequenceNode : Node {
        /// <summary>
        /// List of nodes in sequence.
        /// </summary>
        private readonly List<Node> sequence;

        /// <summary>
        /// Initializes a new instance of the <see cref="SequenceNode"/> class.
        /// </summary>
        public SequenceNode(Specification spec)
            : base(spec) => sequence = new List<Node>();

        /// <summary>
        /// Gets the length of the sequence.
        /// </summary>
        public int Length => sequence.Count;

        /// <summary>
        /// Gets the nth child.
        /// </summary>
        /// <param name="index">Zero-based index.</param>
        /// <returns>Child node.</returns>
        public Node this[int index] => sequence[index];

        /// <summary>
        /// Append a node to the sequence.
        /// </summary>
        public void Append(Node node) {
            sequence.Add(node);
        }

        /// <summary>
        /// Test for equality.  Two sequences are equal if their children are equal.
        /// </summary>
        public override bool Equals(object? node) {
            if (node is SequenceNode sNode) {
                if (sNode.sequence.Count != sequence.Count) {
                    return false;
                }

                for (int i = 0; i < sequence.Count; i++) {
                    if (!sequence[i].Equals(sNode.sequence[i])) {
                        return false;
                    }
                }

                return true;
            } else {
                return false;
            }
        }

        /// <summary>
        /// Trivial GetHashCode implementation.  Needs proper implementation.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode() {
            return base.GetHashCode();
        }

        /// <summary>
        /// Touch the node and all its children.
        /// </summary>
        public override void Touch(TouchFunction touchFunc) {
            touchFunc(this);
            foreach (Node node in sequence) {
                node.Touch(touchFunc);
            }
        }
    }
}