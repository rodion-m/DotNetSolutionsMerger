## Overview
This tool merges multiple .NET solution files into a single solution file. It is designed to be run from the command line and accepts various input options for flexibility.

_The source code is almost entirely written by Claude Sonnet 3.5._

## Installation
Ensure you have .NET SDK installed. Clone or download the repository containing this tool, then build it using the .NET CLI.

Download the repository and navigate to the project directory and run the following command:

```bash
dotnet build DotNetSolutionsMerger.sln
```

Or just download the latest release (dll) from the [Releases](https://github.com/rodion-m/DotNetSolutionsMerger/releases)

## Usage
The tool can be invoked using the command line. Below are the usage options and examples.

### Syntax
```bash
dotnet MergeSolutions.dll --input <input_paths> --output <output_path>
```

### Options
- `--input`, `-i`: Input solution file paths or a directory containing solution files. This option is required and can accept multiple paths.
- `--output`, `-o`: Output path for the merged solution file. This option is required.

### Examples

#### Example 1: Merging multiple solution files
To merge several solution files into a single solution file:
```bash
dotnet MergeSolutions.dll --input solution1.sln solution2.sln solution3.sln --output mergedSolution.sln
```
This command merges `solution1.sln`, `solution2.sln`, and `solution3.sln` into `mergedSolution.sln`.

#### Example 2: Merging all solution files in a directory
To merge all solution files located in a specific directory:
```bash
dotnet MergeSolutions.dll --input /path/to/solutions --output mergedSolution.sln
```
This command merges all solution files found in `/path/to/solutions` directory into `mergedSolution.sln`. 
