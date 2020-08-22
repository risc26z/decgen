// ----------------------------------------------------------------------------
//  file     Program.cs
//  author   Jacob M <risc26z@gmail.com>
//  created  31 Jul 2020
//
//  This file is copyrighted and licensed under the GNU LGPL, version 2.1.
//  There is absolutely no warranty for this software.  See the file COPYING
//  for further details.
// ----------------------------------------------------------------------------

namespace decgen {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using InstDecoderGenerator;
    using Mono.Options;

    /// <summary>
    /// Class for 'decgen', a simple command-line driver application for the InstDecoderGenerator library.
    /// </summary>
    internal class Program {
        private const string version = "0.1";
        private readonly Config config;
        private readonly OptionSet options;
        private string? enumFilename;
        private string flagsStr;

        /// <summary>
        /// Initializes a new instance of the <see cref="Program"/> class.
        /// </summary>
        private Program() {
            config = new Config();

            options = new OptionSet {
                { "v|verbose", "Generate verbose output", v => config.Verbose = true },
                { "t|timings", "Display processing timings", v => config.Timings = true },
                { "e=|generate-enum=", "Set filename for flags enum", v => enumFilename = v },
                { "s=|save-config=", "Save configuration to file and exit", v => SaveConfig(v) },
                { "l=|load-config=", "Load configuration from file", v => LoadConfig(v) },
                { "f=|flags=", "Fix specified flag(s)", v => flagsStr = v },
                { "h|help", "Show this message and exit", v => ShowHelp() }
            };

            flagsStr = "";
            enumFilename = null;
        }

        /// <summary>
        /// Verifies that all rules are reachable via the generated decoder tree, and issues warnings
        /// for each rule that isn't.
        /// </summary>
        private static bool CheckReachability(Node tree) {
            tree.Spec.SetAllMarks(false);

            tree.Touch((Node node) => {
                if (node is Rule) {
                    ((Rule) node).Mark = true;
                }
            });

            bool ret = true;
            tree.Spec.SweepForMarks(false, rule => {
                Console.WriteLine($"Warning: {rule} is unreachable");
                ret = false;
                return false;
            });

            return ret;
        }

        /// <summary>
        /// "Load" and parse flags specification from a string.
        /// </summary>
        private static TristateBitArray LoadFlags(Specification spec, string flags) {
            try {
                return SpecParser.ParseFlags(spec, flags);
            } catch (System.Data.SyntaxErrorException e) {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
                throw;  // unreachable
            }
        }

        /// <summary>
        /// Main program.
        /// </summary>
        private static void Main(string[] args) {
            var program = new Program();
            program.DoMain(args);
        }

        /// <summary>
        /// Build a decoder tree.
        /// </summary>
        private Node BuildTree(Specification spec, TristateBitArray? flags) {
            var stopwatch = Stopwatch.StartNew();
            Node? node = TreeBuilder.BuildTree(spec, flags);
            stopwatch.Stop();

            if (config.Timings) {
                Console.WriteLine($"Generated decoder tree in {stopwatch.ElapsedMilliseconds}ms.");
            }

            return node;
        }

        /// <summary>
        /// Does the actual work of the main program.
        /// </summary>
        private void DoMain(string[] args) {
            List<string> extraArgs = options.Parse(args);

            if (extraArgs.Count != 2) {
                ShowHelp();
            }

            if (config.Verbose) {
                Console.WriteLine($"Loading specification \"{extraArgs[0]}\".");
            }

            Specification spec = LoadSpecification(extraArgs[0]);

            TristateBitArray? flagsBits = null;
            if (flagsStr.Length != 0) {
                flagsBits = LoadFlags(spec, flagsStr);
            }

            Node tree = BuildTree(spec, flagsBits);
            CheckReachability(tree);

            GenerateOutput(tree, extraArgs[1]);
        }

        /// <summary>
        /// Generates output file(s).
        /// </summary>
        private bool GenerateOutput(Node tree, string filename) {
            var stopwatch = Stopwatch.StartNew();
            if (enumFilename != null) {
                using (var enumFile = new StreamWriter(enumFilename)) {
                    var codeBuilder = new CFamilyGenerator(tree.Spec, enumFile);
                    codeBuilder.ProcessEnum();
                }
            }

            using (var file = new StreamWriter(filename)) {
                var codeBuilder = new CFamilyGenerator(tree.Spec, file);
                codeBuilder.ProcessRoot(tree);
            }
            stopwatch.Stop();

            if (config.Timings) {
                Console.WriteLine($"Generated output in {stopwatch.ElapsedMilliseconds}ms.");
            }

            return true;
        }

        /// <summary>
        /// Loads the configuration from a JSON file.
        /// </summary>
        /// <param name="filename"></param>
        private void LoadConfig(string filename) {
            using (var file = new StreamReader(filename)) {
                config.Load(file);
            }
        }

        /// <summary>
        /// Loads the specification from disk.
        /// </summary>
        private Specification LoadSpecification(string filename) {
            var stopwatch = Stopwatch.StartNew();

            Specification spec;
            using (var file = new StreamReader(filename)) {
                spec = new Specification(config);
                try {
                    spec.Load(file);
                } catch (System.Data.SyntaxErrorException e) {
                    Console.WriteLine(e.Message);
                    Environment.Exit(1);
                }
            }

            stopwatch.Stop();

            if (config.Timings) {
                Console.WriteLine($"Loaded specification in {stopwatch.ElapsedMilliseconds}ms.");
            }

            return spec;
        }

        /// <summary>
        /// Save configuration to a JSON file.
        /// </summary>
        private void SaveConfig(string filename) {
            using (var file = new StreamWriter(filename)) {
                config.Save(file);
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Shows help and exits.
        /// </summary>
        private void ShowHelp() {
            Console.WriteLine("Usage: {0} [OPTION]... SOURCE DEST", Process.GetCurrentProcess().ProcessName);
            Console.WriteLine();
            Console.WriteLine("This is decgen version {0} - a binary pattern decoder generator.", version);
            Console.WriteLine("Decgen is Copyright (C) 2020, and licensed under the LGPL version 2.1.");
            Console.WriteLine("There is absolutely no warranty for using this software.");
            Console.WriteLine();

            // output the options
            Console.WriteLine("Options:");
            options.WriteOptionDescriptions(Console.Out);

            Environment.Exit(0);
        }
    }
}