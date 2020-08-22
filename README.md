# decgen
Decgen is a binary instruction decoder generator.  When given an instruction set specification containing pattern-matching rules, it generates code compatible with the C family of languages (C, C++, C#, and so on).

Instruction decoders are found in virtual machines, simulators, emulators, disassemblers, and similar programs.  They are often large and complex, error-prone, and are typically performance hotspots.

Decgen writes them automatically.  It can perform a number of optimisations, many of which can be tuned through configuration settings.


## Status

This is an early version, and bugs should be expected. Decgen has not been thoroughly tested, but it appears to create correct and efficient decoders.

Although the code is reasonably well commented, little documentation exists.

An example specification is included in the decgen/Test directory.

## Example usage

decgen --help

*(Prints command line help.)*

decgen -s config.json

*(Save configuration settings.  The settings can now be edited to fit your preferences.)*

decgen -l config.json specification.txt decoder.cs

*(Load configuration settings, then generate decoder.cs from specification.txt.)*

