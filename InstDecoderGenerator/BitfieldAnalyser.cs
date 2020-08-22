// ----------------------------------------------------------------------------
//  file     BitfieldAnalyser.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  02 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// The bitfield analyser is responsible for examining a <see cref="RuleSet"/>, and producing
    /// from it a <see cref="Bitfield"/> or <see cref="BitfieldSet"/> that divides the rules
    /// according to the value of a set of bits.
    /// </summary>
    internal sealed class BitfieldAnalyser {
        /// <summary>
        /// Calculated quality of each bit. Each value is zero or more, and indicates how well the
        /// bit distinguishes between rules, with higher values indicating better discrimination.
        ///
        /// To get a non-zero quality score, a bit must a) be defined and zero in at least one rule,
        /// b) be defined and one in at least one rule, and c) not defined in the ruleset's
        /// effective condition.
        ///
        /// The ideal bit will be present in every rule that meets condition (c), and will have an
        /// even distribution of ones and zeroes.
        /// </summary>
        private readonly float[] bitQuality;

        /// <summary>
        /// Associated RuleSet.
        /// </summary>
        private readonly RuleSet ruleSet;

        /// <summary>
        /// Index of most significant bit with non-zero quality.
        /// </summary>
        private int maxSignificantBit;

        /// <summary>
        /// Index of least significant bit with non-zero quality.
        /// </summary>
        private int minSignificantBit;

        /// <summary>
        /// Total quality of all bits.
        /// </summary>
        private float totalBitQuality;

        /// <summary>
        /// Initializes a new instance of the <see cref="BitfieldAnalyser"/> class.  Also
        /// causes the per-bit quality ratings to be computed.
        /// </summary>
        /// <param name="set"></param>
        public BitfieldAnalyser(RuleSet set) {
            ruleSet = set;
            bitQuality = new float[Spec.NumBits];

            // precalculate quality ratings for each bit
            CalculateBitQuality();
        }

        /// <summary>
        /// Gets highest-numbered bit index with non-zero quality.
        /// </summary>
        public int MaxSignificantBit => maxSignificantBit;

        /// <summary>
        /// Gets lowest-numbered bit index with non-zero quality.
        /// </summary>
        public int MinSignificantBit => minSignificantBit;

        /// <summary>
        /// Gets associated specification.
        /// </summary>
        public Specification Spec => ruleSet.Spec;

        /// <summary>
        /// Find the best available bitfield within the range of lengths specified.
        /// </summary>
        /// <param name="min">Smallest acceptable number of bits.</param>
        /// <param name="max">Largest acceptable number of bits.</param>
        /// <returns>Bitfield, or null in case of failure.</returns>
        public Bitfield? FindBestBitfield(int min, int max) {
            return FindBestBitfield(min, max, GetIdealNumBits());
        }

        /// <summary>
        /// Find the best available set of bitfields within the range of total lengths specified.
        /// </summary>
        /// <param name="min">Smallest acceptable number of bits.</param>
        /// <param name="max">Largest acceptable number of bits.</param>
        /// <returns>BitfieldSet, or null in case of failure.</returns>
        public BitfieldSet? FindBestBitfieldSet(int min, int max) {
            return FindBestBitfieldSet(min, max, GetIdealNumBits());
        }

        /// <summary>
        /// Access per-bit quality rating.
        /// </summary>
        public float GetBitQuality(int bit) {
            Debug.Assert(bit >= 0 && bit < Spec.NumBits);

            return bitQuality[bit];
        }

        /// <summary>
        /// Find the best single bitfield suitable for use in a switch statement.
        ///
        /// This algorithm is not perfect: it can sometimes return a sub-optimal bitfield. It uses a
        /// heuristic approach, under the assumption that the best bitfield is the same as the
        /// bitfield with the highest total bit quality (and nearest the desired bit length). This
        /// assumption tends to favour longer bitfields. In some cases, however, a shorter bitfield
        /// may have an equal ability to discriminate between rules.
        /// </summary>
        /// <param name="min">Fewest number of acceptable bits.</param>
        /// <param name="max">Largest number of acceptable bits.</param>
        /// <param name="ideal">Preferred number of acceptable bits.</param>
        /// <param name="exclusion">A mask indicating bits that should be excluded (optional).</param>
        /// <returns>The best bitfield meeting the criteria, or null in case of failure.</returns>
        private Bitfield? FindBestBitfield(int min, int max, int ideal, TristateBitArray? exclusion = null) {
            if (exclusion == null) {
                exclusion = ruleSet.Condition.DecodeBits;
            }

            Bitfield? best = null;

            // iterate through candidate start bits
            for (int start = MinSignificantBit; start <= MaxSignificantBit; start++) {
                // iterate through candidate end bits
                for (int end = start + min - 1; end <= start + max - 1; end++) {
                    // discard any bitfield that provably contains zero-quality bits
                    if (end <= MaxSignificantBit) {
                        // viable bitfields cannot intersect with the exclusion mask
                        var bits = TristateBitArray.LoadBitfieldValue(Spec.NumBits, start, end, 0);
                        if (!exclusion.MaskIntersectsWith(bits)) {
                            // calculate total bit quality over the range
                            if (CalculateTotalQuality(start, end, out float totalQuality)) {
                                var tmp = new Bitfield(Spec, start, end, ideal, totalQuality);

                                // track highest overall quality
                                if (best == null || tmp.Quality > best.Quality) {
                                    best = tmp;
                                }
                            }
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Calculate ideal number of bits for a switch statement. This must balance code size
        /// against the potential for subdividing rules bases on their bit patterns.
        /// </summary>
        /// <returns>Preferred number of bits.</returns>
        private int GetIdealNumBits() {
            return (int) Math.Ceiling(Math.Log(ruleSet.NumRules, 2));
        }

        /// <summary>
        /// Calculate the sum of all bit quality scores in a bitfield.
        /// </summary>
        /// <param name="start">Least significant bit.</param>
        /// <param name="end">Most significant bit.</param>
        /// <param name="totalQuality">Calculated sum.</param>
        /// <returns>True if the bitfield is valid.</returns>
        private bool CalculateTotalQuality(int start, int end, out float totalQuality) {
            totalQuality = 0.0F;
            for (int bit = start; bit <= end; bit++) {
                if (GetBitQuality(bit) == 0F) {
                    return false;
                }

                totalQuality += GetBitQuality(bit);
            }

            return true;
        }

        /// <summary>
        /// Generate cache of quality ratings for each bit.  Called by constructor.
        /// </summary>
        private void CalculateBitQuality() {
            // initialise temporary arrays
            int[] total = new int[Spec.NumBits];
            int[] totalOne = new int[Spec.NumBits];
            float[] score = new float[Spec.NumBits];

            // iterate through each Rule in the RuleSet
            foreach (RuleSetEntry entry in ruleSet) {
                for (int i = 0; i < Spec.NumBits; i++) {
                    // is this bit significant to the rule after taking the ruleset's
                    // condition into account?
                    if (entry.EffectiveCondition.DecodeBits.GetMaskBit(i)) {
                        // accumulate totals
                        total[i]++;

                        // calculate total score, taking into account the Rule's weight
                        TristateBitArray tmp = entry.EffectiveCondition.Flags;

                        if (!tmp.IsEmpty) {
                            score[i] += entry.Rule.Weight * Spec.Config.BitFlagCoef;
                        } else {
                            score[i] += entry.Rule.Weight;
                        }

                        // tally up bits which must equal one
                        if (entry.EffectiveCondition.DecodeBits.GetValueBit(i)) {
                            totalOne[i]++;
                        }
                    }
                }
            }

            // calculate total of all individual bit scores
            float totalScore = 0F;
            for (int i = 0; i < Spec.NumBits; i++) {
                totalScore += score[i];
            }

            for (int i = 0; i < Spec.NumBits; i++) {
                // calculate smaller of total ones or zeroes
                int totalZero = total[i] - totalOne[i];
                int totalSmaller = (totalZero < totalOne[i]) ? totalZero : totalOne[i];

                // quantify, on 0-1 scale, how well balanced the values are.
                // that is, how evenly distributed are ones and zeroes?  a bit that is set for
                // every rule is utterly useless for a switch, since all rules would fall into
                // a single case.
                float balance = 2F * (totalSmaller / (float) total[i]);

                // calculate overall bit quality
                float bitQ;
                if (score[i] != 0F) {
                    bitQ = balance * score[i] / totalScore;
                } else {
                    bitQ = 0F;  // avoid NaNs in calculation
                }

                // store calculated quality in cache
                bitQuality[i] = bitQ;

                // accumulate totals
                totalBitQuality += bitQ;
                if (bitQ != 0F) {
                    // track least significant bit
                    if (i < minSignificantBit) {
                        minSignificantBit = i;
                    }

                    // track most significant bit
                    if (i > maxSignificantBit) {
                        maxSignificantBit = i;
                    }
                }
            }
        }

        /// <summary>
        /// Find the best set of bitfields that meet the supplied criteria.
        /// </summary>
        /// <param name="min">Fewest number of acceptable bits.</param>
        /// <param name="max">Largest number of acceptable bits.</param>
        /// <param name="ideal">Preferred number of bits.</param>
        /// <returns>The best result, or null in case of failure.</returns>
        private BitfieldSet? FindBestBitfieldSet(int min, int max, int ideal) {
            if (Spec.Config.MaxSwitchSplits == 0) {
                return null;
            }

            BitfieldSet? best = null;

            // iterate through allowed numbers of bitfields in result
            for (int fields = 2; fields <= Spec.Config.MaxSwitchSplits + 1; fields++) {
                BitfieldSet? bfSet = FindBestBitfieldSet(min, max, ideal, fields);

                // track best result
                if (best == null || (bfSet != null && bfSet.Quality > best.Quality)) {
                    best = bfSet;
                }
            }

            return best;
        }

        /// <summary>
        /// Find the best set of bitfields. This method uses a recursive algorithm to build
        /// the set.
        /// </summary>
        private BitfieldSet? FindBestBitfieldSet(int min, int max, int ideal, int fields) {
            if (fields > 0) {
                BitfieldSet? best = null;

                // calculate maximum number of bits in THIS bitfield (which must leave at
                // least one bit for each of the remaining fields).
                int maxBits = max - (fields - 1);

                // iterate through number of possible bits in THIS bitfield
                for (int bits = 1; bits <= maxBits; bits++) {
                    // recursively find the solution to the problem, excluding this bitfield
                    BitfieldSet? bfSet = FindBestBitfieldSet(1, max - bits, ideal, fields - 1);
                    if (bfSet == null) {
                        return null;
                    }

                    // generate an exclusion mask for the bits in bfSet (since the bitfields in
                    // the set must not overlap)
                    TristateBitArray exclusion = new TristateBitArray(
                        ruleSet.Condition.DecodeBits).Union(bfSet.GetBitsForValue(0));

                    // find the best bitfield to add to the sub-solution
                    Bitfield? bf = FindBestBitfield(bits, bits, bits, exclusion);
                    if (bf != null) {
                        // add the bitfield to the result
                        bfSet.Add(bf);

                        // track best solution
                        if (best == null || bfSet.Quality > best.Quality) {
                            best = bfSet;
                        }
                    }
                }

                // return the best solution
                return best;
            } else {
                // return an empty set
                return new BitfieldSet(Spec, ideal);
            }
        }
    }
}