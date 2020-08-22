// ----------------------------------------------------------------------------
//  file     Specification.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  31 Jul 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// Represents a subset of the <seealso cref="Rule"/> s in a Specification. This set matches a
    /// specified <seealso cref="Condition"/>; other rules may also be excluded (eg., if this or a
    /// predecessor has been created via ExcludeLast(). RuleSets may be derived from other RuleSets;
    /// the effect of this is to successively apply each Condition so as to limit the included Rules.
    /// </summary>
    internal sealed class RuleSet : IEnumerable<RuleSetEntry> {
        /// <summary>
        /// Associated condition.  Every rule is guaranteed to match this.
        /// </summary>
        private readonly Condition condition;

        /// <summary>
        /// Cached list of entries in this RuleSet.
        /// </summary>
        private readonly List<RuleSetEntry> entries;

        /// <summary>
        /// Associated specification.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleSet"/> class.
        /// </summary>
        /// <param name="spec">Associated Specification object.</param>
        public RuleSet(Specification spec) {
            this.spec = spec;
            condition = new Condition(spec,
                new TristateBitArray(spec.NumBits),
                new TristateBitArray(spec.NumFlags));

            entries = new List<RuleSetEntry>();
            foreach (Rule rule in spec.Rules) {
                AddRule(rule);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleSet"/> class.
        /// </summary>
        /// <param name="parent">RuleSet from which information should be inherited.</param>
        /// <param name="condition">Condition to apply as a filter to select rules.</param>
        private RuleSet(RuleSet parent, Condition condition) {
            spec = parent.spec;
            this.condition = condition;
            entries = new List<RuleSetEntry>();
        }

        /// <summary>
        /// Gets the ruleset's associated condition.
        /// </summary>
        public Condition Condition => condition;

        /// <summary>
        /// Gets the number of rules in the ruleset.
        /// </summary>
        public int NumRules => entries.Count;

        /// <summary>
        /// Gets associated specification.
        /// </summary>
        public Specification Spec => spec;

        /// <summary>
        /// Indexer for accessing rules by zero-based index.
        /// </summary>
        public RuleSetEntry this[int index] {
            get {
                Debug.Assert(index >= 0 && index < entries.Count);
                return entries[index];
            }
        }

        /// <summary>
        /// Creates a derived RuleSet where the effective condition is the
        /// union of the current condition and the specified condition.
        /// </summary>
        /// <param name="condition">Condition for derived RuleSet.</param>
        /// <returns>The derivative RuleSet.</returns>
        public RuleSet Derive(Condition condition) {
            var child = new RuleSet(this, condition.Union(this.condition));

            child.PopulateRules(entries);
            return child;
        }

        /// <summary>
        /// Create a derived RuleSet without the last Rule in this RuleSet.
        /// The condition is unchanged.
        /// </summary>
        /// <returns>The derived RuleSet.</returns>
        public RuleSet DeriveExcludingLast() {
            Debug.Assert(NumRules >= 1);

            var child = new RuleSet(this, condition);
            child.PopulateRules(entries, entries[NumRules - 1]);
            return child;
        }

        /// <summary>
        /// IEnumerable&lt;Rule&gt; interface.
        /// </summary>
        public IEnumerator<RuleSetEntry> GetEnumerator() {
            return entries.GetEnumerator();
        }

        /// <summary>
        /// IEnumerable interface.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() {
            return entries.GetEnumerator();
        }

        /// <summary>
        /// Add a rule to the cache.
        /// </summary>
        /// <param name="rule">Rule to be added.</param>
        /// <returns>True if the rule is an exact match.</returns>
        private bool AddRule(Rule rule) {
            var entry = new RuleSetEntry(rule, this);
            entries.Add(entry);
            if (entry.EffectiveCondition.IsEmpty) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Populate cached <seealso cref="RuleSetEntry"/>(s) with the contents of an
        /// arbitrary container.
        /// </summary>
        /// <param name="list">Container from which to extract Rules.</param>
        /// <param name="except">Rule to exclude (optional).</param>
        private void PopulateRules(IEnumerable<RuleSetEntry> list, RuleSetEntry? except = null) {
            // iterate through all rules
            foreach (RuleSetEntry entry in list) {
                if (entry != except) {
                    // add the rule if it can match our associated condition
                    if (condition.IsCompatible(entry.Rule.Condition)) {
                        // add the rule.  if it is an exact match for the condition,
                        // omit any further rules since they can never match.
                        if (AddRule(entry.Rule)) {
                            break;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// An entry in a RuleSet.  Each entry is a wrapper around an associated Rule.
    /// </summary>
    internal class RuleSetEntry {
        /// <summary>
        /// Effective condition in the ruleset.
        /// </summary>
        private readonly Condition effectiveCondition;

        /// <summary>
        /// Associated rule.
        /// </summary>
        private readonly Rule rule;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuleSetEntry"/> class.
        /// </summary>
        /// <param name="rule">Associated Rule.</param>
        /// <param name="ruleSet">Associated RuleSet</param>
        public RuleSetEntry(Rule rule, RuleSet ruleSet) {
            this.rule = rule;

            // calculate the effective condition of the rule in this ruleset.
            effectiveCondition = rule.Condition.SubtractIntersection(ruleSet.Condition);
        }

        /// <summary>
        /// Gets the rule's condition after taking the ruleset's condition into account.
        /// </summary>
        public Condition EffectiveCondition => effectiveCondition;

        /// <summary>
        /// Gets the encapsulated rule.
        /// </summary>
        public Rule Rule => rule;
    }
}