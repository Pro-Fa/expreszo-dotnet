using BenchmarkDotNet.Running;

// Dispatch to any benchmark class by name, or run all when called with no args:
//   dotnet run -c Release
//   dotnet run -c Release -- --filter *ParsingBenchmarks*
//   dotnet run -c Release -- --list flat
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

public static partial class Program;
