// ----------------------------------------------------------------------------
//  file     Config.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  02 Aug 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace InstDecoderGenerator {
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.Json;

    /// <summary>
    /// Holds configuration data describing how the code should function.
    /// </summary>
    public sealed class Config {
        /// <summary>
        /// Initializes a new instance of the <see cref="Config"/> class.
        /// </summary>
        public Config() {
            Timings = false;
            Verbose = false;
            AllowSwitch = true;
            AllowSequence = true;
            InsertReturns = false;
            NoPrettyOutput = false;
            NoOptimiseIfConditionNodes = false;
            NoBreakAfterRule = true;
            BitFlagCoef = 1.0F;
            BitfieldLengthDeltaPower = 0.5F;
            BitfieldSetLengthDeltaPower = 0.5F;
            BitfieldSetCoef = 1.0F;
            MinSwitchRules = 4;
            MinSwitchBits = 2;
            MaxSwitchBits = 8;
            MaxSwitchNestingDepth = 3;
            MaxTotalSwitchBits = 15;
            MaxSwitchSplits = 1;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to allow generation of node sequences. This is
        /// valid if and only if the code to process matched Rules always return from the decoder
        /// function. If it is valid, it can often generate better code.
        /// </summary>
        public bool AllowSequence { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to allow generation of switch blocks. For
        /// generating production decoders, this should generally be true.
        /// </summary>
        public bool AllowSwitch { get; set; }

        /// <summary>
        /// Gets or sets a value specifying the constant P, as used in bitfield quality
        /// calculations. Bitfield quality is calculated uding (as the denominator) (delta + 1)^P
        /// where P is a constant, and defined here. Typical value is 0.5F (square root).
        /// </summary>
        public float BitfieldLengthDeltaPower { get; set; }

        /// <summary>
        /// Gets or sets a value specifying the coefficient for bitfield <em>set</em> quality. The
        /// calculated quality of a bitfield set is multiplied by this value prior to comparison
        /// with the best single bitfield. Thus, values between 0 and 1 will act to discourage use
        /// of bitfield sets. On a modern superscalar core, an extra bitfield extraction is fairly
        /// cheap, and worth using if it will reduce the amount of difficult-to-predict branching
        /// needed, as mispredicted branches are very costly.
        /// </summary>
        public float BitfieldSetCoef { get; set; }

        /// <summary>
        /// Gets or sets the constant used in calculating bitfield set quality. This property is the
        /// same as <see cref="BitfieldLengthDeltaPower"/>, the only difference being the single
        /// bitfield. See documentation on that property for details.
        /// </summary>
        public float BitfieldSetLengthDeltaPower { get; set; }

        /// <summary>
        /// Gets or sets the constant used when calculating bit quality using rules with non-empty
        /// effective flags. Under such circumstances, it isn't possible to select rules using a
        /// switch alone; the flags must still be tested. This coefficient is used to adjust the
        /// weight of those rules.
        /// </summary>
        public float BitFlagCoef { get; set; }

        /// <summary>
        /// Gets or sets a value to indicate whether 'return;' should be inserted after every
        /// rule's code fragment. This is a convenient way to ensure valid conditions for
        /// <see cref="AllowSequence"/>. See also <seealso cref="NoBreakAfterRule"/>.
        /// </summary>
        public bool InsertReturns { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of bits in a switch expression. large values produce
        /// huge switch statements and correspondingly large code size. On the other hand, small
        /// switch statements are inefficient as execution must pass through deep chains of
        /// condition testing.
        /// </summary>
        public int MaxSwitchBits { get; set; }

        /// <summary>
        /// Gets or sets the maximum nesting depth of switch statements in the generated
        /// output.
        /// </summary>
        public int MaxSwitchNestingDepth { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of times the bits in a bitfield set may be 'split'. This
        /// is the maximum number of bitfields in a bitfield set minus one. To disable bitfield
        /// sets, set this value to zero.
        /// </summary>
        public int MaxSwitchSplits { get; set; }

        /// <summary>
        /// Gets or sets the maximum total bits used as expressions in switch statements at any
        /// given point in the decoder tree.
        /// </summary>
        public int MaxTotalSwitchBits { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of bits in a switch expression. Switches are compiled to
        /// an indirect jump instruction, which typically has poor branch prediction behaviour.
        /// Consequently, a small number of if statements can often be faster than a switch. This
        /// figure should be high enough to compensate.
        /// </summary>
        public int MinSwitchBits { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of rules in a rule set before we consider using a switch.
        /// </summary>
        public int MinSwitchRules { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to inhibit automatic generation of 'break' statements
        /// after simple rules in switch cases.  These statements are not required if they would be
        /// unreachable, for example if every rule includes a 'return' statement.
        /// </summary>
        public bool NoBreakAfterRule { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to disable simplification of 'if' conditions in
        /// decoder tree.
        /// </summary>
        public bool NoOptimiseIfConditionNodes { get; set; }

        /// <summary> Gets or sets a flag indicating whether to omit indentation & comments in
        /// output. This should be fractionally quicker to both generate and (once the output is
        /// generated) to compile. However, the generated output will be less easy to read. </summary>
        public bool NoPrettyOutput { get; set; }

        /// <summary> Gets or sets a flag indicating whether to time processing stages &amp; dump
        /// results to stdout. </summary>
        public bool Timings { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to enable generation of copious diagnostic output
        /// during processing.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>
        /// Load the configuration from a JSON file.
        /// </summary>
        /// <param name="reader">Stream containing JSON data.</param>
        public void Load(StreamReader reader) {
            string contents = reader.ReadToEnd();
            Config tmp = JsonSerializer.Deserialize<Config>(contents);

            // copy public properties
            foreach (PropertyInfo property in typeof(Config).GetProperties().Where(p => p.CanWrite)) {
                property.SetValue(this, property.GetValue(tmp, null), null);
            }
        }

        /// <summary>
        /// Save the configuration to a JSON file.
        /// </summary>
        /// <param name="writer">Stream to receive JSON data.</param>
        public void Save(StreamWriter writer) {
            var serializeOptions = new JsonSerializerOptions {
                WriteIndented = true
            };
            string contents = JsonSerializer.Serialize<Config>(this, serializeOptions);
            writer.Write(contents);
        }
    }
}