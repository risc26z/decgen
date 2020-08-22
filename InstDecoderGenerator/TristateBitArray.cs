// ----------------------------------------------------------------------------
//  file     TristateBitArray.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  12 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.Diagnostics;
    using System.Text;

    /// <summary>
    /// An arbitrarily-sized array of binary digits, each of which can hold
    /// either zero (false), one (true), or an undefined value.
    ///
    /// Internally, the array is a pair of arrays of unsigned 64-bit
    /// integers - the <em>mask</em> (holding one for bits with defined
    /// values, and zero for undefined bits), and the <em>value</em>, which
    /// holds the value of the bit if defined, or zero otherwise.  This
    /// internal representation is exposed through the public interface.
    /// </summary>
    public class TristateBitArray {
        /// <summary>
        /// Mask data.
        /// </summary>
        private readonly ulong[] mask;

        /// <summary>
        /// Number of bits in the array.
        /// </summary>
        private readonly int numBits;

        /// <summary>
        /// Number of ulongs in the mask[] and value[] arrays.
        /// </summary>
        private readonly int numUlongs;

        /// <summary>
        /// Value data.
        /// </summary>
        private readonly ulong[] value;

        /// <summary>
        /// Initializes a new instance of the <see cref="TristateBitArray"/> class.
        /// </summary>
        /// <param name="numBits">Number of bits in the array.</param>
        public TristateBitArray(int numBits) {
            Debug.Assert(numBits > 0);

            this.numBits = numBits;
            numUlongs = (numBits + 0x3f) >> 6;
            mask = new ulong[numUlongs];
            value = new ulong[numUlongs];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TristateBitArray"/> class.
        /// </summary>
        /// <param name="copy">The bit array to copy.  This is not modified.</param>
        public TristateBitArray(TristateBitArray copy) {
            numBits = copy.numBits;
            numUlongs = copy.numUlongs;
            mask = (ulong[]) copy.mask.Clone();
            value = (ulong[]) copy.value.Clone();
        }

        /// <summary>
        /// Represents the possible states of a bit.
        /// </summary>
        public enum BitValue {
            Zero,       // bit must be zero
            One,        // bit must be one
            Undefined   // bit may hold any value
        }

        /// <summary>
        /// Gets a value indicating whether indicates whether the array is 'empty' - that is, if all bits are undefined.
        /// </summary>
        public bool IsEmpty {
            get {
                // TristateBitArray is empty if mask is all zeroes, so just iterate
                // through ulongs and check that they're all zero
                for (int i = 0; i < numUlongs; i++) {
                    if (mask[i] != 0) {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the number of non-undefined bits in the array.
        /// </summary>
        public int NumSignificantBits {
            get {
                int count = 0;
                for (int i = 0; i < numBits; i++) {
                    if (GetMaskBit(i)) {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// Gets the number of ulongs in the internal representation of the bit array.
        /// </summary>
        public int NumUlongs => numUlongs;

        /// <summary>
        /// Create a new array, with a consecutive series of bits (up to 64) with defined values.
        /// All other bits are left at their default undefined state.
        /// </summary>
        /// <param name="numBits">Number of bits in the resulting array.</param>
        /// <param name="start">Zero-based index of least significant bit in bitfield.</param>
        /// <param name="end">Index of most significant bit in bitfield (inclusive).</param>
        /// <param name="value">
        /// Contains values of bits to be inserted. If the bitfield represents fewer than 64 bits,
        /// these bits are found in the least significant bits of 'value'.
        /// </param>
        /// <returns></returns>
        public static TristateBitArray LoadBitfieldValue(int numBits, int start, int end, ulong value) {
            Debug.Assert(numBits > 0 && start >= 0 &&
                end < numBits && end >= start && (1 + end - start) <= 64);

            var tmp = new TristateBitArray(numBits);
            tmp.MergeBitfieldValue(start, end, value);
            return tmp;
        }

        /// <summary>
        /// Extract the mask for a given bit index. As the mask is essentially a boolean indicating
        /// whether the bit is defined, this method might easily be renamed IsBitDefined().
        /// </summary>
        /// <param name="index">Zero-based bit index.</param>
        /// <returns>Whether the bit is defined.</returns>
        public bool GetMaskBit(int index) {
            Debug.Assert(index >= 0 && index < numBits);
            return (mask[index >> 6] & (1UL << (index & 0x3f))) != 0;
        }

        /// <summary>
        /// Extract the nth mask ulong.
        /// </summary>
        /// <param name="index">Zero-based ulong index.</param>
        /// <returns>Value of ulong.</returns>
        public ulong GetMaskUlong(int index) {
            Debug.Assert(index >= 0 && index < numUlongs);
            return mask[index];
        }

        /// <summary>
        /// Extract the given bit's value.  This is zero if the bit is undefined; otherwise
        /// it is the defined bit's value.
        /// </summary>
        /// <param name="index">Zero-based bit index.</param>
        /// <returns>The bit's value, if defined, or zero otherwise.</returns>
        public bool GetValueBit(int index) {
            Debug.Assert(index >= 0 && index < numBits);
            return (value[index >> 6] & (1UL << (index & 0x3f))) != 0;
        }

        /// <summary>
        /// Extract the nth value ulong.
        /// </summary>
        /// <param name="index">Zero-based ulong index.</param>
        /// <returns>Value of ulong.</returns>
        public ulong GetValueUlong(int index) {
            Debug.Assert(index >= 0 && index < numUlongs);
            return value[index];
        }

        /// <summary>
        /// Compute the intersection of two arrays.  The intersection of two bits is undefined
        /// if either or both bits are undefined, or the bit's value if both are defined.
        /// </summary>
        /// <param name="rhs">Compatible array to compare with.</param>
        /// <returns>The resulting bit array.</returns>
        public TristateBitArray Intersection(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            var tmp = new TristateBitArray(this);
            for (int i = 0; i < numUlongs; i++) {
                ulong maskIntersection = tmp.mask[i] & rhs.mask[i];
                tmp.mask[i] = maskIntersection;
                tmp.value[i] &= maskIntersection;
                Debug.Assert(tmp.value[i] == (rhs.value[i] & maskIntersection));
            }

            return tmp;
        }

        /// <summary>
        /// Indicate whether two arrays are 'compatible' with each other. Two arrays are compatible
        /// if the bits with definite values in both arrays have the same values. Put another way,
        /// they are compatible if they are equal in the intersection of their masks.
        /// </summary>
        /// <param name="rhs">The array to compare with. Must have the same number of bits.</param>
        /// <returns>True if the arrays are compatible.</returns>
        public bool IsCompatible(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            // iterate through ulongs, checking values for equality within the
            // intersection of the masks
            for (int i = 0; i < numUlongs; i++) {
                ulong maskIntersection = mask[i] & rhs.mask[i];
                if ((value[i] & maskIntersection) != (rhs.value[i] & maskIntersection)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Indicates whether two arrays are exactly equal in value.
        /// </summary>
        /// <param name="rhs">
        /// The array to compare with. Both arrays must have the same number of bits.
        /// </param>
        /// <returns>True if arrays are equal.</returns>
        public bool IsEqual(TristateBitArray rhs) {
            // make sure we're comparing like with like
            Debug.Assert(numBits == rhs.numBits);

            // iterate through ulongs, checking mask & value for equality
            for (int i = 0; i < numUlongs; i++) {
                if (mask[i] != rhs.mask[i] || value[i] != rhs.value[i]) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Tests whether there are any bits that are defined in both arrays. Or, whether the
        /// intersection between both arrays' masks is non-zero.
        /// </summary>
        /// <param name="rhs">Compatible array to compare with. Must have the same number of bits.</param>
        /// <returns></returns>
        public bool MaskIntersectsWith(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            // iterate through ulongs, checking masks for intersections
            for (int i = 0; i < numUlongs; i++) {
                if ((mask[i] & rhs.mask[i]) != 0) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Modify a bit in the array.
        /// </summary>
        /// <param name="index">Zero-based index of bit.</param>
        /// <param name="bitValue">New value.</param>
        public void SetBit(int index, BitValue bitValue) {
            Debug.Assert(index >= 0 && index < numBits);

            int ulongIndex = index >> 6;
            int bitIndex = index & 0x3f;

            switch (bitValue) {
            case BitValue.Zero:
                mask[ulongIndex] |= 1UL << bitIndex;
                value[ulongIndex] &= ~(1UL << bitIndex);
                break;

            case BitValue.One:
                mask[ulongIndex] |= 1UL << bitIndex;
                value[ulongIndex] |= 1UL << bitIndex;
                break;

            case BitValue.Undefined:
                mask[ulongIndex] &= ~(1UL << bitIndex);
                value[ulongIndex] &= ~(1UL << bitIndex);
                break;
            }
        }

        /// <summary>
        /// Compute the difference between two arrays. The difference is undefined if the right-hand
        /// side is defined, or the bit's value otherwise.
        /// </summary>
        /// <param name="rhs">Compatible array to compare with.</param>
        /// <returns>The resulting bit array.</returns>
        public TristateBitArray Subtract(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            // iterate through ulongs, computing the difference
            var tmp = new TristateBitArray(this);
            for (int i = 0; i < numUlongs; i++) {
                Debug.Assert((tmp.value[i] & rhs.mask[i]) == rhs.value[i]);

                ulong maskRhsInverse = ~rhs.mask[i];
                tmp.mask[i] &= maskRhsInverse;
                tmp.value[i] &= maskRhsInverse;
            }

            return tmp;
        }

        /// <summary>
        /// Optimised method for computing x.Subtract(x.Intersection(y)). This method does not need
        /// to create a temporary array, and is thus more efficient.
        /// </summary>
        /// <param name="rhs">Compatible array to subtract from this array.</param>
        /// <returns>The resulting bit array.</returns>
        public TristateBitArray SubtractIntersection(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            var tmp = new TristateBitArray(this);
            for (int i = 0; i < numUlongs; i++) {
                ulong maskIntersection = tmp.mask[i] & rhs.mask[i];
                Debug.Assert((tmp.value[i] & maskIntersection) == (rhs.value[i] & maskIntersection));

                tmp.mask[i] &= ~maskIntersection;
                tmp.value[i] &= ~maskIntersection;
            }

            return tmp;
        }

        /// <summary>
        /// Format the bit array for output. The format is essentially the same as that of pottern
        /// bits in the specification file, with bits represented as 0, 1, or '.' for undefined
        /// bits. A space is inserted every fourth bit for readability.
        /// </summary>
        public override string ToString() {
            var builder = new StringBuilder();

            // step down from most to least significant bit
            for (int i = numBits - 1; i >= 0; i--) {
                // include a space after every fourth bit, counting from the least
                // significant (right-most) bit.
                if (((i % 4) == 3) && i != numBits - 1) {
                    builder.Append(' ');
                }

                if (GetMaskBit(i)) {
                    builder.Append(GetValueBit(i) ? '1' : '0');
                } else {
                    builder.Append('.');
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Compute the union of two arrays. The union of two bits is undefined if both bits are
        /// undefined, or the value of the bit if either is defined. Since the arrays must be
        /// compatible, a bit defined in both must have the same value.
        /// </summary>
        /// <param name="rhs">Compatible array to compare with.</param>
        /// <returns>The resulting bit array.</returns>
        public TristateBitArray Union(TristateBitArray rhs) {
            Debug.Assert(numBits == rhs.numBits);

            // iterate through ulongs, computing union
            var tmp = new TristateBitArray(this);
            for (int i = 0; i < numUlongs; i++) {
                ulong maskIntersection = tmp.mask[i] & rhs.mask[i];
                Debug.Assert((tmp.value[i] & maskIntersection) == (rhs.value[i] & maskIntersection));

                tmp.mask[i] |= rhs.mask[i];
                tmp.value[i] |= rhs.value[i];
            }

            return tmp;
        }

        /// <summary>
        /// Internal method used by LoadBitfieldValue. Straightforward recursive algorithm to load a
        /// ulong into the array, allowing for the possibility of a split across adjacent ulongs.
        /// </summary>
        private void MergeBitfieldValue(int start, int end, ulong v) {
            int loUlongIndex = start >> 6;
            int hiUlongIndex = end >> 6;
            if (loUlongIndex == hiUlongIndex) {
                int length = 1 + end - start;
                mask[loUlongIndex] |= ((1UL << length) - 1) << (start & 0x3f);
                value[loUlongIndex] |= v << (start & 0x3f);
            } else {
                int hiStart = (start + 0x3f) & ~0x3f;
                MergeBitfieldValue(start, hiStart - 1, v);
                MergeBitfieldValue(hiStart, end, v >> (hiStart - start));
            }
        }
    }
}