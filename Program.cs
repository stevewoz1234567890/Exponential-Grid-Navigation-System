using System.Numerics;
using TwoDimentionalSpaceGame;

static void PrintUsage()
{
    Console.WriteLine(
        """
        Exponential Grid Navigation

        Usage:
          dotnet run -- [options]

        Options:
          --start <BigInteger>   Starting X coordinate (default: 1024)
          --mlr <BigInteger>     Tolerance: max distance to a power of 2 per axis (default: 10)
          --out <directory>      Write registry.json, history.json, pos.json
          --name <string>        Label stored in pos.json (default: run)
          -h, --help             Show this help

        Example:
          dotnet run -- --start 1048576 --mlr 50 --out ./output --name demo
        """);
}

var argv = args.ToList();
if (argv.Contains("-h") || argv.Contains("--help"))
{
    PrintUsage();
    return;
}

BigInteger start = 1024;
BigInteger mlr = 10;
string? outDir = null;
string name = "run";

try
{
    for (int i = 0; i < argv.Count; i++)
    {
        var a = argv[i];
        if (a == "--start")
            start = BigInteger.Parse(argv[++i]);
        else if (a == "--mlr")
            mlr = BigInteger.Parse(argv[++i]);
        else if (a == "--out")
            outDir = argv[++i];
        else if (a == "--name")
            name = argv[++i];
        else if (!a.StartsWith('-'))
            start = BigInteger.Parse(a);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Invalid arguments: {ex.Message}");
    PrintUsage();
    Environment.ExitCode = 1;
    return;
}

var outcome = ExponentialGridNavigator.Run(start, mlr, name);
Console.WriteLine(
    $"Reverse path verified. {outcome.StepCount} steps: ({outcome.StartX},{outcome.StartY}) -> ({outcome.FinalX},{outcome.FinalY}); MLR={outcome.MLR}");

if (outDir is not null)
{
    ExponentialGridNavigator.WriteRunArtifacts(outcome, outDir);
    Console.WriteLine($"Wrote registry.json, history.json, pos.json to {Path.GetFullPath(outDir)}");
}
