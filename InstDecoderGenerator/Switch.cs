// ----------------------------------------------------------------------------
//  file     Bitfield.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  08 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// Decoder tree representation of a switch statement.
    /// </summary>
    public class SwitchNode : Node {
        /// <summary>
        /// Array of nodes for each 'case'.
        /// </summary>
        private readonly Node[] children;

        /// <summary>
        /// Node for the switch value.
        /// </summary>
        private readonly SwitchableNode expression;

        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchNode"/> class.
        /// </summary>
        public SwitchNode(Specification spec, SwitchableNode expression)
            : base(spec) {
            this.expression = expression;
            children = new Node[expression.NumValues];
        }

        /// <summary>
        /// Gets the node for the expression.
        /// </summary>
        public SwitchableNode Expression => expression;

        /// <summary>
        /// Gets or sets the node for the nth case.
        /// </summary>
        /// <param name="index">Corresponding value of switch expression.</param>
        /// <returns>The node.</returns>
        public Node this[int index] {
            get => children[index];
            set => children[index] = value;
        }

        /// <summary>
        /// Tests for equality. A switch node matches another switch node if it has the same
        /// expression and the same children.
        /// </summary>
        public override bool Equals(object? n) {
            if (n is SwitchNode node) {
                if (!expression.Equals(node.expression)) {
                    return false;
                }

                for (int i = 0; i < expression.NumValues; i++) {
                    if (!children[i].Equals(node.children[i])) {
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
        /// Touch the node and its children.
        /// </summary>
        /// <param name="touchFunc">Function to call for each node.</param>
        public override void Touch(TouchFunction touchFunc) {
            touchFunc(this);
            expression.Touch(touchFunc);
            for (int i = 0; i < expression.NumValues; i++) {
                children[i].Touch(touchFunc);
            }
        }
    }

    /// <summary>
    /// Base class for objects that can be used as expressions for a 'switch' node.
    /// Generally, derived classes will reference one or more bits of the
    /// instruction word, and will define transformations between a switchable integer
    /// and the corresponding bit values.
    /// </summary>
    public abstract class SwitchableNode : Node {
        /// <summary>
        /// Initializes a new instance of the <see cref="SwitchableNode"/> class.
        /// </summary>
        protected SwitchableNode(Specification spec)
            : base(spec) {
        }

        /// <summary>
        /// Gets total number of bits in expression.
        /// </summary>
        public abstract int NumBits { get; }

        /// <summary>
        /// Gets the number of possible values that the expression can take.
        /// </summary>
        public int NumValues => 1 << NumBits;

        /// <summary>
        /// Map switch expression value to a particular arrangement of decode bits.
        /// </summary>
        /// <param name="value">Value of switch expression (0-NumValues).</param>
        /// <returns>Bit array with applicable bits set to their respective values.</returns>
        public abstract TristateBitArray GetBitsForValue(int value);
    }

    /// <summary>
    /// Representation of a bitfield - a contiguous range of bits - within an instruction word. The
    /// range of bits runs from 'start' (a zero-based bit index) to 'end'. The range is inclusive:
    /// that is, the bit numbered 'end' is included within the bit range. Thus, a single bit is
    /// specified as start==end, and it is impossible to specify a zero-length bitfield.
    /// </summary>
    public sealed class Bitfield : SwitchableNode {
        /// <summary>
        /// Lowest-numbered bit.
        /// </summary>
        private readonly int start;

        /// <summary>
        /// Highest-numbered bit (inclusive).
        /// </summary>
        private readonly int end;

        /// <summary>
        /// Target bitfield length when this was found.
        /// </summary>
        private readonly int ideal;

        /// <summary>
        /// Overall assessed quality.
        /// </summary>
        private readonly float quality;

        /// <summary>
        /// Sum of bit qualities for included bits.
        /// </summary>
        private readonly float totalBitQuality;

        /// <summary>
        /// Initializes a new instance of the <see cref="Bitfield"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="start">Least significant bit.</param>
        /// <param name="end">Most significant bit.</param>
        /// <param name="ideal">Target number of bits for this bitfield.</param>
        /// <param name="totalBitQuality">Sum of quality scores for each included bit.</param>
        public Bitfield(Specification spec,
                        int start,
                        int end,
                        int ideal,
                        float totalBitQuality)
            : base(spec) {
            // sanity-check arguments
            Debug.Assert(start >= 0 && end >= start && end < spec.NumBits &&
                ideal >= 0 && ideal < spec.NumBits);

            this.start = start;
            this.end = end;
            this.ideal = ideal;
            this.totalBitQuality = totalBitQuality;

            quality = CalculateQuality();
        }

        /// <summary>
        /// Gets index of most significant bit in bitfield (inclusive).
        /// </summary>
        public int End => end;

        /// <summary>
        /// Gets number of bits in bitfield.
        /// </summary>
        public override int NumBits => 1 + end - start;

        /// <summary>
        /// Gets quality score for entire bitfield.
        /// </summary>
        public float Quality => quality;

        /// <summary>
        /// Gets index of least significant bit in bitfield.
        /// </summary>
        public int Start => start;

        /// <summary>
        /// Gets sum of per-bit quality scores.
        /// </summary>
        public float TotalBitQuality => totalBitQuality;

        /// <summary>
        /// Trivial equality comparison.
        /// </summary>
        public override bool Equals(object? n) {
            if (n is Bitfield node) {
                return start == node.start && end == node.end;
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
        /// Very simple implementation of GetBitsForValue: just return a bitarray loaded
        /// with the value.
        /// </summary>
        public override TristateBitArray GetBitsForValue(int value) {
            return TristateBitArray.LoadBitfieldValue(Spec.NumBits, start, end, (ulong) value);
        }

        /// <summary>
        /// Conversion to string (used in diagnostics).
        /// </summary>
        public override string ToString() {
            if (start != end) {
                return $"{start}:{end}";
            } else {
                return $"{start}";
            }
        }

        /// <summary>
        /// Calculate overall bitfield quality.  See <seealso cref="Config"/>  for the formula.
        /// </summary>
        /// <returns>Quality rating.</returns>
        private float CalculateQuality() {
            float bits = 1 + end - start;
            float diffBits = Math.Abs(ideal - bits);
            float denominator = (float) Math.Pow(1 + diffBits, Spec.Config.BitfieldLengthDeltaPower);

            return totalBitQuality / denominator;
        }
    }

    /// <summary>
    /// Representation of a sequence of bitfields. A BitfieldSet may temporarily be empty, such as
    /// while it is being generated, but under normal circumstances it should contain one or more
    /// <seealso cref="Switch"/> s.
    /// </summary>
    public sealed class BitfieldSet : SwitchableNode {
        /// <summary>
        /// Child bitfields.
        /// </summary>
        private readonly List<Bitfield> bitfields;

        /// <summary>
        /// Preferred total bit count.
        /// </summary>
        private readonly int ideal;

        /// <summary>
        /// Total bit count.
        /// </summary>
        private int numBits;

        /// <summary>
        /// Overall quality.
        /// </summary>
        private float quality;

        /// <summary>
        /// Total bit quality.
        /// </summary>
        private float totalBitQuality;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitfieldSet"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="ideal">Preferred total bit length.</param>
        public BitfieldSet(Specification spec, int ideal)
            : base(spec) {
            Debug.Assert(ideal >= 0 && ideal < spec.NumBits);

            bitfields = new List<Bitfield>();
            this.ideal = ideal;
        }

        /// <summary>
        /// Gets list of bitfields in set.
        /// </summary>
        public List<Bitfield> Bitfields => bitfields;

        /// <summary>
        /// Gets total number of bits.
        /// </summary>
        public override int NumBits => numBits;

        /// <summary>
        /// Gets quality score.
        /// </summary>
        public float Quality => quality;

        /// <summary>
        /// Append a bitfield to the set.
        /// </summary>
        /// <param name="bitfield">Bitfield to be added.</param>
        public void Add(Bitfield bitfield) {
            bitfields.Add(bitfield);

            numBits += bitfield.NumBits;
            totalBitQuality += bitfield.TotalBitQuality;

            // recalculate quality
            float diffBits = Math.Abs(ideal - numBits);
            float denominator = (float) Math.Pow(1 + diffBits, Spec.Config.BitfieldSetLengthDeltaPower);

            quality = Spec.Config.BitfieldSetCoef * totalBitQuality / denominator;
        }

        /// <summary>
        /// Test for equality with another bitfield set.  Bitfield sets are equal if they
        /// contain equal bitfields.
        /// </summary>
        public override bool Equals(object? n) {
            // same number of bitfields?
            if (n is BitfieldSet node) {
                if (bitfields.Count != node.bitfields.Count) {
                    return false;
                }

                // do the bitfields themselves match?
                for (int i = 0; i < bitfields.Count; i++) {
                    if (!bitfields[i].Equals(bitfields[i])) {
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
        /// Obtain bit array for a given value of a switch expression. This is achieved by
        /// decomposing the value into bitfields, then letting each bitfield build a bit array, and
        /// finally merging each bit array together.
        /// </summary>
        /// <param name="value">Value of switch expression.</param>
        /// <returns>Bit array containing corresponding bits.</returns>
        public override TristateBitArray GetBitsForValue(int value) {
            var condition = new TristateBitArray(Spec.NumBits);

            // iterate through bitfields, merging each together to form composite
            // bit array
            int offset = 0;
            foreach (Bitfield bitfield in bitfields) {
                // we consider the whole 'value' field to be itself composed of
                // bitfields stacked back-to-back.  so to get the 'value' as this
                // bitfield sees it, we need to extract its bits from the composite 'value'.
                int bfValue = (value >> offset) & ((1 << bitfield.NumBits) - 1);

                condition = condition.Union(bitfield.GetBitsForValue(bfValue));

                offset += bitfield.NumBits;
            }

            return condition;
        }

        /// <summary>
        /// Obtain a textual representation of the bitfield set, suitable for diagnostics.
        /// </summary>
        public override string ToString() {
            var builder = new StringBuilder();
            int i = 0;
            foreach (Bitfield bf in bitfields) {
                // insert separator, if required
                if (i != 0) {
                    builder.Append(", ");
                }

                builder.Append(bf.ToString());
                i++;
            }

            return builder.ToString();
        }
    }
}