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
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// In-memory representation of a flag.
    /// </summary>
    public class Flag : Node {
        private readonly int index;
        private readonly bool isDummy;
        private readonly string name;

        /// <summary>
        /// Initializes a new instance of the <see cref="Flag"/> class.
        /// </summary>
        public Flag(Specification spec, string name, int index, bool isDummy)
            : base(spec) {
            this.name = name;
            this.index = index;
            this.isDummy = isDummy;
        }

        /// <summary>
        /// Gets the 0-based index of the flag, which is also its bit number in the bitwise
        /// representation of flags.
        /// </summary>
        public int Index => index;

        /// <summary>
        /// Gets a boolean representing whether the flag is 'real' or a dummy flag.
        /// </summary>
        public bool IsDummy => isDummy;

        /// <summary>
        /// Gets the flag's identifier in string form.
        /// </summary>
        public override string ToString() {
            return name;
        }
    }

    /// <summary>
    /// In-memory representation of a pattern rule.
    /// </summary>
    public class Rule : Node {
        /// <summary>
        /// Source code to be generated upon match.
        /// </summary>
        private readonly CodeFragment codeFragment;

        /// <summary>
        /// Match condition.
        /// </summary>
        private readonly Condition condition;

        /// <summary>
        /// Line number of start of rule's declaration.
        /// </summary>
        private readonly int lineNum;

        /// <summary>
        /// Associated spec.
        /// </summary>
        private readonly Specification spec;

        /// <summary>
        /// Rule weight (used by bitfield analyser to optimise for common cases).
        /// </summary>
        private readonly float weight;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rule"/> class.
        /// </summary>
        public Rule(Specification spec,
                    Condition condition,
                    CodeFragment codeFragment,
                    float weight,
                    int lineNum)
            : base(spec) {
            this.spec = spec;
            this.condition = condition;
            this.codeFragment = codeFragment;
            this.weight = weight;
            this.lineNum = lineNum;
            Mark = false;
        }

        /// <summary>
        /// Gets the Rule's code fragment.
        /// </summary>
        public CodeFragment CodeFragment => codeFragment;

        /// <summary>
        /// Gets the Rule's condition.
        /// </summary>
        public Condition Condition => condition;

        /// <summary>
        /// Gets the line number of the start of the Rule.
        /// </summary>
        public int LineNum => lineNum;

        /// <summary>
        /// Gets or sets the 'mark'.
        /// </summary>
        public bool Mark { get; set; }

        /// <summary>
        /// Gets the rule's weight.
        /// </summary>
        public float Weight => weight;

        /// <summary>
        /// Gets the rule's diagnostic information.
        /// </summary>
        public override string ToString() {
            return $"Rule at line {lineNum}";
        }
    }

    /// <summary>
    /// In-memory representation of a decoder specification.  The class is
    /// responsible for loading the specification from disc.
    /// </summary>
    public sealed class Specification {
        /// <summary>
        /// Code fragment (enum file epilogue).
        /// </summary>
        private readonly CodeFragment enumEndFrag;

        /// <summary>
        /// Code fragment (enum file prologue).
        /// </summary>
        private readonly CodeFragment enumStartFrag;

        /// <summary>
        /// Code fragment to include as file epilogue.
        /// </summary>
        private readonly CodeFragment fileEndFrag;

        /// <summary>
        /// Code fragment to include as file prologue.
        /// </summary>
        private readonly CodeFragment fileStartFrag;

        /// <summary>
        /// Code fragment to include to fetch decode flags.
        /// </summary>
        private readonly CodeFragment decodeFlagsFrag;

        /// <summary>
        /// Code fragment to include to fetch instruction word.
        /// </summary>
        private readonly CodeFragment fetchFrag;

        /// <summary>
        /// Lookup table for flags, with random access by name.
        /// </summary>
        private readonly Dictionary<string, Flag> flagDict;

        /// <summary>
        /// Table of Flags, with random access by index.
        /// </summary>
        private readonly List<Flag> flagTable;

        /// <summary>
        /// Table of Rules, with random access by index.
        /// </summary>
        private readonly List<Rule> ruleTable;

        /// <summary>
        /// Current configuration settings.
        /// </summary>
        private readonly Config config;

        /// <summary>
        /// Initializes a new instance of the <see cref="Specification"/> class.
        /// </summary>
        /// <param name="config">Configuration settings.</param>
        public Specification(Config config) {
            this.config = config;

            ruleTable = new List<Rule>();
            flagTable = new List<Flag>();
            flagDict = new Dictionary<string, Flag>();
            NumBits = 0;
            RootIndentation = 0;

            fileStartFrag = new CodeFragment(this);
            fileEndFrag = new CodeFragment(this);

            enumStartFrag = new CodeFragment(this);
            enumEndFrag = new CodeFragment(this);

            decodeFlagsFrag = new CodeFragment(this);
            fetchFrag = new CodeFragment(this);

            // create a dummy flag so that we don't try to work with 0-sized
            // tristatebitarrays.
            AddFlag("<dummy>", true);
        }

        /// <summary>
        /// Gets the associated configuration object.
        /// </summary>
        public Config Config => config;

        /// <summary>
        /// Gets the code fragment for the end of an enum file.
        /// </summary>
        public CodeFragment EnumEnd => enumEndFrag;

        /// <summary>
        /// Gets the indentation depth of an enum.
        /// </summary>
        public int EnumIndentation { get; set; }

        /// <summary>
        /// Gets the code fragment for the start of an enum file.
        /// </summary>
        public CodeFragment EnumStart => enumStartFrag;

        /// <summary>
        /// Gets the code fragment for the end of a file.
        /// </summary>
        public CodeFragment FileEnd => fileEndFrag;

        /// <summary>
        /// Gets the code fragment for the start of a file.
        /// </summary>
        public CodeFragment FileStart => fileStartFrag;

        /// <summary>
        /// Gets the code fragment for accessing the flags in generated code.
        /// </summary>
        public CodeFragment DecodeFlags => decodeFlagsFrag;

        /// <summary>
        /// Gets the code fragment for an instruction word fetch.
        /// </summary>
        public CodeFragment FetchFrag => FetchFrag;

        /// <summary>
        /// Gets a boolean indicating whether the specification has any real flags.
        /// </summary>
        public bool HasFlags => !(NumFlags == 1 && flagTable[0].IsDummy);

        /// <summary>
        /// Gets or sets number of decode bits in rules.
        /// </summary>
        public int NumBits { get; set; }

        /// <summary>
        /// Gets number of declared flags.
        /// </summary>
        public int NumFlags => flagTable.Count;

        /// <summary>
        /// Gets or sets the indentation depth of the root node.
        /// </summary>
        public int RootIndentation { get; set; }

        /// <summary>
        /// Gets the rule table.
        /// </summary>
        public IEnumerable<Rule> Rules => ruleTable;

        /// <summary>
        /// Create a new flag and add it to internal tables.
        /// </summary>
        /// <param name="name">Identifier of the flag.</param>
        /// <param name="isDummy">True if this is a dummy flag (external
        /// callers should never set this to true).</param>
        public void AddFlag(string name, bool isDummy = false) {
            if (!HasFlags) {
                // delete dummy flag
                flagDict.Remove(flagTable[0].ToString());
                flagTable.Remove(flagTable[0]);
            }

            Debug.Assert(!flagDict.ContainsKey(name));

            var flag = new Flag(this, name, flagDict.Count, isDummy);
            flagDict.Add(name, flag);
            flagTable.Add(flag);
        }

        /// <summary>
        /// Add a new rule to the specification.
        /// </summary>
        /// <param name="rule">Rule to add.</param>
        public void AddRule(Rule rule) {
            ruleTable.Add(rule);
        }

        /// <summary>
        /// Retrieve a flag by zero-based index.
        /// </summary>
        public Flag GetFlag(int index) {
            Debug.Assert(index >= 0 && index < NumFlags);

            return flagTable[index];
        }

        /// <summary>
        /// Retrieve a flag by its identifier.
        /// </summary>
        public Flag? GetFlag(string name) {
            if (flagDict.TryGetValue(name, out Flag? flag)) {
                return flag;
            } else {
                return null;
            }
        }

        /// <summary>
        /// Load specification from file or resource.
        /// </summary>
        public void Load(StreamReader textReader) {
            SpecParser.Parse(this, textReader);
        }

        /// <summary>
        /// Initialise all rules' 'mark' flag to a given value.
        /// </summary>
        /// <param name="value"></param>
        public void SetAllMarks(bool value) {
            foreach (Rule rule in ruleTable) {
                rule.Mark = value;
            }
        }

        /// <summary>
        /// Search rule table for rules with 'mark' flag == hitValue.  For each of
        /// these rules, execute sweepFunc, and stop if it returns true.
        /// </summary>
        /// <param name="hitValue">Value of 'mark' to search for.</param>
        /// <param name="sweepFunc">Function to call.</param>
        public void SweepForMarks(bool hitValue, Func<Rule, bool> sweepFunc) {
            foreach (Rule rule in ruleTable) {
                if (rule.Mark == hitValue) {
                    if (sweepFunc(rule)) {
                        break;
                    }
                }
            }
        }
    }
}