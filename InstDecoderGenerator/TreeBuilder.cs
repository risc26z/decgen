// ----------------------------------------------------------------------------
//  file     TreeBuilder.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  01 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System;
    using System.Diagnostics;

    /// <summary>
    /// TreeBuilder is the class responsible for generating the decoder's
    /// logic, expressed as a tree of objects derived from <see cref="Node"/>.
    /// </summary>
    public sealed class TreeBuilder {
        /// <summary>
        /// Associated bitfield analyser.
        /// </summary>
        private readonly BitfieldAnalyser analyser;

        /// <summary>
        /// Associated ruleset.
        /// </summary>
        private readonly RuleSet ruleSet;

        /// <summary>
        /// Associated spec.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// Total switch depth so far.
        /// </summary>
        private int switchNestingDepth;

        /// <summary>
        /// Total number of switchable bits used so far.
        /// </summary>
        private int totalSwitchBits;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeBuilder"/> class.
        /// </summary>
        /// <param name="spec">Associated specification.</param>
        /// <param name="filter">Associated ruleset.</param>
        /// <param name="parent">
        /// Optional parent TreeBuilder. If present, tallies of switch bits and depth will be
        /// inherited from it.
        /// </param>
        private TreeBuilder(Specification spec, RuleSet filter, TreeBuilder? parent = null) {
            this.spec = spec;
            ruleSet = filter;

            analyser = new BitfieldAnalyser(ruleSet);

            // if there is a parent TreeBuilder, inherit switch information from it
            if (parent != null) {
                switchNestingDepth = parent.switchNestingDepth;
                totalSwitchBits = parent.totalSwitchBits;
            } else {
                switchNestingDepth = 0;
                totalSwitchBits = 0;
            }
        }

        /// <summary>
        /// The sole publicly-visible entry point.  Generates a tree, given a Specification.
        /// </summary>
        /// <param name="spec">The decoder specification.</param>
        /// <returns>The root of the decoder tree.</returns>
        public static Node BuildTree(Specification spec, TristateBitArray? flags) {
            if (flags == null) {
                flags = new TristateBitArray(spec.NumFlags);
            }

            // initialise a TreeBuilder with an empty condition and a RuleSet
            // including all rules.
            var initialCondition = new Condition(spec, new TristateBitArray(spec.NumBits), flags);
            RuleSet ruleSet = new RuleSet(spec).Derive(initialCondition);
            var builder = new TreeBuilder(spec, ruleSet);

            return builder.Build();
        }

        /// <summary>
        /// The main internal entry point.  Recursively called to build a node tree.
        /// </summary>
        /// <returns>The root of the decoder tree (not null).</returns>
        private Node Build() {
            if (spec.Config.Verbose) {
                Console.WriteLine("Building decode tree for {0}", ruleSet.Condition.ToString());
            }

            // try several strategies for building a tree and return the results of
            // the first that is successful
            Node? node = null;
            for (int i = 0; node == null; i++) {
                node = i switch
                {
                    0 => BuildEmpty(),
                    1 => BuildFallbackSequence(),
                    2 => BuildLiftFlags(),
                    3 => BuildLiftDecodeBits(),
                    4 => BuildInvertedPair(),
                    5 => BuildSwitch(),
                    6 => BuildSequence(),
                    7 => BuildIfChain(),
                    _ => throw new Exception()
                };
            }

            return node;
        }

        /// <summary>
        /// Build an empty node, if the RuleSet is empty.
        /// </summary>
        private Node? BuildEmpty() {
            if (ruleSet.NumRules != 0) {
                return null;
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Building empty node.");
            }

            return new EmptyNode(spec);
        }

        /// <summary>
        /// The last Rule is often a 'fallback' rule with an empty condition. If this is the case,
        /// we can sometimes perform an optimisation that reduces the overall code size.
        /// </summary>
        /// <returns>Root of the generated tree.</returns>
        private Node? BuildFallbackSequence() {
            // sanity check
            if (ruleSet.NumRules <= 1) {
                return null;
            }

            // check that there is a fallback rule
            RuleSetEntry lastRule = ruleSet[ruleSet.NumRules - 1];
            if (!lastRule.EffectiveCondition.IsEmpty) {
                return null;
            }

            // check that sequence nodes are permitted
            if (!spec.Config.AllowSequence) {
                return null;
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Performing fallback sequence optimisation.");
            }

            // remove the fallback rule and build the tree
            RuleSet rSet = ruleSet.DeriveExcludingLast();
            var builder = new TreeBuilder(spec, rSet, this);
            Node node = builder.Build();

            // ensure that there is a sequence node including the root
            // of the tree
            SequenceNode seq;
            if (node is SequenceNode) {
                seq = (SequenceNode) node;
            } else {
                seq = new SequenceNode(spec);
                seq.Append(node);
            }

            // now paste the fallback rule onto the end of the sequence
            seq.Append(lastRule.Rule);
            return seq;
        }

        /// <summary>
        /// Try to improve efficiency by 'lifting' a flags test higher up the decoder tree. This is
        /// only done if the flags test is identical for every member of a rule set.
        /// </summary>
        private Node? BuildLiftFlags() {
            return BuildLift("flags",
                cond => cond.Flags,
                flags => new Condition(spec, new TristateBitArray(spec.NumBits), flags));
        }

        /// <summary>
        /// Try to improve efficiency by 'lifting' a decode test higher up the decoder tree. This is
        /// only done if the decode test is identical for every member of a rule set.
        /// </summary>
        private Node? BuildLiftDecodeBits() {
            return BuildLift("decode bits",
                cond => cond.DecodeBits,
                bits => new Condition(spec, bits, new TristateBitArray(spec.NumBits)));
        }

        /// <summary>
        /// Try to improve efficiency by 'lifting' a test higher up the decoder tree. This is
        /// only done if the test is identical for every member of a rule set.
        /// </summary>
        private Node? BuildLift(string name, Func<Condition, TristateBitArray> extract, Func<TristateBitArray, Condition> init) {
            if (ruleSet.NumRules < 2) {
                return null;
            }

            // extract component from 1st rule & verify that flags are specified
            TristateBitArray component = extract(ruleSet[0].EffectiveCondition);
            if (component.IsEmpty) {
                return null;
            }

            // check that all other rules have the same component spec
            for (int i = 1; i < ruleSet.NumRules; i++) {
                if (!extract(ruleSet[i].EffectiveCondition).IsEqual(component)) {
                    return null;
                }
            }

            if (spec.Config.Verbose) {
                Console.WriteLine($"Performing {name} lifting optimisation.");
            }

            // build subtree without flags
            Condition cond = init(component);
            RuleSet rSet = ruleSet.Derive(cond);
            var builder = new TreeBuilder(spec, rSet, this);
            Node node = builder.Build();

            // and attach it to an if-node
            return new IfElseNode(spec, cond, node, new EmptyNode(spec));
        }

        /// <summary>
        /// A pair testing both values of a single bit may be replaced with a single if node
        /// (rather than one for each value).
        /// </summary>
        private Node? BuildInvertedPair() {
            // check that we've got exactly two rules
            if (ruleSet.NumRules != 2) {
                return null;
            }

            // check that both rules specify a single bit
            if (ruleSet[0].EffectiveCondition.DecodeBits.NumSignificantBits != 1 ||
                ruleSet[1].EffectiveCondition.DecodeBits.NumSignificantBits != 1) {
                return null;
            }

            // check that neither rule specifies effective flags
            if (!ruleSet[0].EffectiveCondition.Flags.IsEmpty ||
                !ruleSet[1].EffectiveCondition.Flags.IsEmpty) {
                return null;
            }

            // check that it's the same single bit
            if (!ruleSet[0].EffectiveCondition.DecodeBits.MaskIntersectsWith(ruleSet[1].EffectiveCondition.DecodeBits)) {
                return null;
            }

            return new IfElseNode(spec,
                ruleSet[0].EffectiveCondition,
                ruleSet[0].Rule,
                ruleSet[1].Rule);
        }

        /// <summary>
        /// Generate a chain of if (cond) { rule } else if ... nodes. This is the 'natural'
        /// structure for a decoder tree, as an entire specification is logically an if-else chain
        /// in this format. All other trees are just optimisations that should be functionally equivalent.
        /// </summary>
        private Node BuildIfChain() {
            if (spec.Config.Verbose) {
                Console.WriteLine("Building if-else chain for {0} rules.", ruleSet.NumRules);
            }

            // the final rule won't have an else branch
            Node previous = new EmptyNode(spec);

            // iterate in reverse through the RuleSet's rules
            for (int i = ruleSet.NumRules - 1; i >= 0; i--) {
                // construct an if-else node with the 'else' branch pointing
                // to the previously-processed rule
                previous = CreateIfElseNode(ruleSet[i], previous);
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Finished building if-else chain.");
            }

            // return node corresponding to first rule in ruleset
            return previous;
        }

        /// <summary>
        /// Build the tree as a sequential series of 'if' nodes. Provided that the code generated
        /// for each Rule is guaranteed to return, this is functionally equivalent to an if-else chain.
        /// </summary>
        private Node? BuildSequence() {
            // check that sequence nodes are permitted
            if (!spec.Config.AllowSequence) {
                return null;
            }

            if (ruleSet.NumRules < 2) {
                return null;
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Building sequence for {0} rules.", ruleSet.NumRules);
            }

            var node = new SequenceNode(spec);

            // iterate through Rules
            foreach (RuleSetEntry entry in ruleSet) {
                // append an if-else node to the sequence
                node.Append(CreateIfElseNode(entry, new EmptyNode(spec)));
            }

            return node;
        }

        /// <summary>
        /// Try to generate a switch node, using either a bitfield or a bitfield set as the switch
        /// expression (whichever has higher quality). Switches are built recursively, so 'case'
        /// labels might contain other switches, or ifchains - whatever Build() decides.
        /// </summary>
        private Node? BuildSwitch() {
            if (!IsSwitchPermitted()) {
                return null;
            }

            // calculate maximum number of bits to include in bitfield(s)
            int maxBits = Math.Min(
                 spec.Config.MaxTotalSwitchBits - totalSwitchBits,
                 spec.Config.MaxSwitchBits);

            // make sure number of bits is valid
            if (maxBits < spec.Config.MinSwitchBits) {
                return null;
            }

            if (spec.Config.Verbose) {
                // dump bit quality table
                for (int i = 0; i < spec.NumBits; i++) {
                    Console.WriteLine("Bit {0}: quality {1}", i, analyser.GetBitQuality(i));
                }
            }

            // actually build the switch based on the better expression
            // (either a bitfield or bitfield set)
            SwitchableNode? expression = FindBestSwitchable(maxBits);
            if (expression != null) {
                return BuildSwitch(expression);
            } else {
                return null;
            }
        }

        /// <summary>
        /// Actually build the switch node, given the supplied expression.
        /// </summary>
        /// <param name="expression">Either a bitfield or a bitfield set.</param>
        /// <returns>Bitfield node object.</returns>
        private SwitchNode BuildSwitch(SwitchableNode expression) {
            if (spec.Config.Verbose) {
                Console.WriteLine("Building switch node for {0}.", expression.ToString());
            }

            var node = new SwitchNode(spec, expression);

            // iterate through every possible value of 'expression'
            for (int value = 0; value < expression.NumValues; value++) {
                // store child in switch node's table
                node[value] = BuildSwitchCase(node, value);
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Switch completed.");
            }

            return node;
        }

        /// <summary>
        /// Build a decoder subtree for the given switch case.
        /// </summary>
        /// <param name="node">The enclosing switch node.</param>
        /// <param name="value">Value of case (eg., case 0x0 has value 0).</param>
        /// <returns>Decoder subtree.</returns>
        private Node BuildSwitchCase(SwitchNode node, int value) {
            Debug.Assert(value >= 0 && value < node.Expression.NumValues);

            if (spec.Config.Verbose) {
                Console.WriteLine("Switch case {0}", value);
            }

            // generate a derived RuleSet for this switch case
            var cond = new Condition(spec,
                node.Expression.GetBitsForValue(value),
                new TristateBitArray(spec.NumFlags));

            RuleSet derivedSet = ruleSet.Derive(cond);

            // generate a corresponding TreeBuilder and update its totals for
            // switch bits & nesting depth
            var builder = new TreeBuilder(spec, derivedSet, this);
            builder.totalSwitchBits += node.Expression.NumBits;
            builder.switchNestingDepth++;

            // use it to build tree for case label
            Node child = builder.Build();

            // search previously-generated switch cases for functionally identical subtrees
            for (int previousValue = 0; previousValue < value; previousValue++) {
                if (node[previousValue].Equals(child)) {
                    if (spec.Config.Verbose) {
                        Console.WriteLine("Pruning child {0} => {1}", value, previousValue);
                    }

                    // replace generated subtree with a reference to the (identical) subtree
                    child = new ChildReferenceNode(spec, previousValue);
                    break;
                }
            }

            return child;
        }

        /// <summary>
        /// Generate an if-else node for a Rule.
        /// </summary>
        private Node CreateIfElseNode(RuleSetEntry entry, Node elseBranch) {
            // generate a child node for the rule
            if (entry.EffectiveCondition.IsEmpty) {
                if (spec.Config.Verbose && !(elseBranch is EmptyNode)) {
                    Console.WriteLine("Discarding non-empty else branch; condition is empty.");
                }

                return entry.Rule;
            } else {
                if (spec.Config.NoOptimiseIfConditionNodes) {
                    return new IfElseNode(spec, entry.Rule.Condition, entry.Rule, elseBranch);
                } else {
                    return new IfElseNode(spec, entry.EffectiveCondition, entry.Rule, elseBranch);
                }
            }
        }

        /// <summary>
        /// Find the best Bitfield and the best BitfieldSet, and choose whichever has the highest quality.
        /// </summary>
        /// <param name="maxBits">Maximum number of bits to select.</param>
        /// <returns></returns>
        private SwitchableNode? FindBestSwitchable(int maxBits) {
            Debug.Assert(maxBits >= 0 && maxBits < spec.NumBits);

            // find a single bitfield on which we can switch
            Bitfield? bitfield = analyser.FindBestBitfield(spec.Config.MinSwitchBits, maxBits);

            if (bitfield == null) {
                // if we can't find even a low-quality single bitfield,
                // we won't find a split one, so just fail
                return null;
            }

            if (spec.Config.Verbose) {
                Console.WriteLine("Best single bitfield has quality {0}", bitfield.Quality);
            }

            SwitchableNode node = bitfield;

            // find a split bitfield
            BitfieldSet? bitfieldSet = analyser.FindBestBitfieldSet(spec.Config.MinSwitchBits, maxBits);

            if (bitfieldSet != null) {
                if (spec.Config.Verbose) {
                    Console.WriteLine("Best bitfield set has quality {0}", bitfieldSet.Quality);
                }

                // select either the single or the split bitfield, depending on
                // which has the highest quality
                if (bitfieldSet.Quality > bitfield.Quality) {
                    node = bitfieldSet;
                }
            }

            return node;
        }

        /// <summary>
        /// Test whether a switch may be generated.
        /// </summary>
        private bool IsSwitchPermitted() {
            // check we're allowed to include switch nodes
            if (!spec.Config.AllowSwitch) {
                return false;
            }

            // check we've enough rules to warrant a switch (with very small rulesets,
            // switches won't be worthwhile as a series of if-nodes will be more efficient)
            if (ruleSet.NumRules < spec.Config.MinSwitchRules) {
                return false;
            }

            // check that we haven't exceeded the allowed switch nesting depth
            if (switchNestingDepth > spec.Config.MaxSwitchNestingDepth) {
                return false;
            }

            return true;
        }
    }
}