// ----------------------------------------------------------------------------
//  file     Condition.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  01 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System;
    using System.Text;

    /// <summary>
    /// Represents the combination of a bit pattern and a flag pattern. Either of these may
    /// be wholly unspecified.
    /// </summary>
    public sealed class Condition : Node {
        /// <summary>
        /// Decode bit pattern.
        /// </summary>
        private TristateBitArray decodeBits;

        /// <summary>
        /// Flags pattern.
        /// </summary>
        private TristateBitArray flags;

        /// <summary>
        /// Initializes a new instance of the <see cref="Condition"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="bitwise">Decode bit pattern.</param>
        /// <param name="flags">Flags pattern.</param>
        public Condition(Specification spec, TristateBitArray bitwise, TristateBitArray flags)
            : base(spec) {
            decodeBits = new TristateBitArray(bitwise);
            this.flags = new TristateBitArray(flags);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Condition"/> class.
        /// Copy constructor.
        /// </summary>
        /// <param name="copy">Bit array to be duplicated.</param>
        public Condition(Condition copy)
            : this(copy.Spec, copy.decodeBits, copy.flags) {
        }

        /// <summary>
        /// Gets decode bit pattern.
        /// </summary>
        public TristateBitArray DecodeBits => decodeBits;

        /// <summary>
        /// Gets flags specification in bit array form.
        /// </summary>
        public TristateBitArray Flags => flags;

        /// <summary>
        /// Gets a value indicating whether neither the decode bits nor the flags have any defined bits.
        /// </summary>
        public bool IsEmpty => decodeBits.IsEmpty && flags.IsEmpty;

        /// <summary>
        /// Determines whether this condition is identical to another condition.
        /// </summary>
        /// <param name="n">Object for comparison.</param>
        /// <returns>True if the object is the same as this Condition.</returns>
        public override bool Equals(object? n) {
            if (n is Condition node) {
                return flags.IsEqual(node.flags) && decodeBits.IsEqual(node.decodeBits);
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
        /// Convert flags to 'friendly' representation, suitable for diagnostics
        /// or embedded comments in generated source code.  This form is essentially
        /// the same as that in flags specifications in the 'specification' file.
        /// </summary>
        /// <returns>Formatted string.</returns>
        public string GetFriendlyFlags() {
            var builder = new StringBuilder();

            // iterate through bits in the flags array.  Each bit corresponds to a flag.
            for (int i = 0; i < Spec.NumFlags; i++) {
                if (flags.GetMaskBit(i)) {
                    // insert ',' separator, if required
                    if (builder.Length != 0) {
                        builder.Append(",");
                    }

                    // is the flag inverted?
                    if (!flags.GetValueBit(i)) {
                        builder.Append("!");
                    }

                    // add the flag's name
                    builder.Append(Spec.GetFlag(i).ToString());
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Convert to 'friendly' string representation, suitable for embedding
        /// as a comment in generated code.
        /// </summary>
        /// <returns>Condition formatted as a string.</returns>
        public string GetFriendlyString() {
            var builder = new StringBuilder();
            if (!DecodeBits.IsEmpty) {
                builder.Append(decodeBits.ToString());
            }

            if (!Flags.IsEmpty) {
                if (builder.Length != 0) {
                    builder.Append(" ");
                }

                builder.AppendFormat("[{0}]", GetFriendlyFlags());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Determines whether this condition is compatible with another condition.
        /// </summary>
        /// <param name="cond">Condition to compare with.</param>
        /// <returns>True if the two conditions are compatible.</returns>
        public bool IsCompatible(Condition cond) {
            return decodeBits.IsCompatible(cond.decodeBits) &&
                flags.IsCompatible(cond.flags);
        }

        /// <summary>
        /// Subtract the intersection between two Conditions.  The intersection is
        /// subtracted from 'this'.  Methods for Subtract and Intersection are not
        /// currently implemented; if they were, this method would be the
        /// equivalent of x.Subtract(x.Intersection(y)).
        /// </summary>
        /// <param name="rhs">Other operand.</param>
        /// <returns>A new Condition representing the calculation result.</returns>
        public Condition SubtractIntersection(Condition rhs) {
            var tmp = new Condition(this);

            tmp.decodeBits = tmp.decodeBits.SubtractIntersection(rhs.decodeBits);
            tmp.flags = tmp.flags.SubtractIntersection(rhs.flags);

            return tmp;
        }

        /// <summary>
        /// Convert to string representation, suitable for use in diagnostics.
        /// </summary>
        /// <returns>Condition formatted as a string.</returns>
        public override string ToString() {
            var builder = new StringBuilder();
            builder.Append(decodeBits.ToString());

            builder.Append(" [");
            builder.Append(flags.ToString());
            builder.Append("]");

            return builder.ToString();
        }

        /// <summary>
        /// Compute the union of two conditions: any bits (in both decode and flags) that
        /// are specified in either Condition.
        /// </summary>
        /// <param name="rhs">Other operand.</param>
        /// <returns>A new Condition representing the calculation result.</returns>
        public Condition Union(Condition rhs) {
            var tmp = new Condition(this);

            tmp.decodeBits = tmp.decodeBits.Union(rhs.decodeBits);
            tmp.flags = tmp.flags.Union(rhs.flags);

            return tmp;
        }
    }
}