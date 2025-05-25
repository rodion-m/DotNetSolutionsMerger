using DotNetSolutionsMerger;
using System.CommandLine;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Merge multiple .NET solution files into a single solution.");

        var inputOption = new Option<string[]>(
            aliases: new[] { "--input", "-i" },
            description: "Input solution file paths or directory containing solution files.")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = true
        };

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Output path for the merged solution file")
        {
            IsRequired = true
        };

        rootCommand.AddOption(inputOption);
        rootCommand.AddOption(outputOption);

        rootCommand.SetHandler((string[] inputs, string output) =>
        {
            try
            {
                if (inputs.Length == 1 && Directory.Exists(inputs[0]))
                {
                    // Input is a directory
                    SolutionMerger.MergeSolutions(inputs[0], output);
                }
                else
                {
                    // Input is a list of solution files
                    SolutionMerger.MergeSolutions(inputs, output);
                }
                Console.WriteLine($"Solutions merged successfully. Output: {output}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }, inputOption, outputOption);

        return await rootCommand.InvokeAsync(args);
    }
}
