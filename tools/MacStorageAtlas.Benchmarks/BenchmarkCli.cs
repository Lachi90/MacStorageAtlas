using System.Text.Json;
using MacStorageAtlas.Core;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Benchmarks;

public sealed class BenchmarkCli
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;

    public BenchmarkCli(TextWriter output, TextWriter error)
    {
        _output = output;
        _error = error;
    }

    public async Task<int> RunAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken = default)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            WriteUsage(_output);
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "fixture" => await RunFixtureCommandAsync(
                    args.Skip(1).ToArray(),
                    cancellationToken),
                "run" => await RunBenchmarkCommandAsync(
                    args.Skip(1).ToArray(),
                    cancellationToken),
                _ => UnknownCommand(args[0])
            };
        }
        catch (ArgumentException exception)
        {
            await _error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (OperationCanceledException)
        {
            await _error.WriteLineAsync("Benchmark command cancelled.");
            return 130;
        }
    }

    private async Task<int> RunFixtureCommandAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            WriteFixtureUsage(_output);
            return 0;
        }

        if (args[0] != "representative")
        {
            return UnknownCommand($"fixture {args[0]}");
        }

        var options = ParseOptions(args.Skip(1));
        var root = Required(options, "root");
        var fixture = await RepresentativeFixtureGenerator.CreateAsync(
            root,
            cancellationToken);

        await WriteJsonAsync(fixture, options);
        await _output.WriteLineAsync(
            $"Created representative fixture at {fixture.RootPath}");
        return 0;
    }

    private async Task<int> RunBenchmarkCommandAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var options = ParseOptions(args);
        var fixtureKind = ParseFixtureKind(ValueOrDefault(options, "fixture", "existing"));
        var measurementMode = ParseMeasurementMode(ValueOrDefault(
            options,
            "mode",
            "shared-aware"));
        var runCount = ParsePositiveInt(ValueOrDefault(options, "runs", "1"), "runs");
        var syntheticFiles = ParsePositiveLong(
            ValueOrDefault(options, "synthetic-files", "1000000"),
            "synthetic-files");
        var cancelAfterProgress = TryParsePositiveInt(
            ValueOrDefault(options, "cancel-after-progress", null),
            "cancel-after-progress");
        var scanOptions = new ScanOptions
        {
            IncludeHiddenFiles = HasFlag(options, "include-hidden"),
            FollowSymbolicLinks = HasFlag(options, "follow-symlinks"),
            TreatPackagesAsDirectories = !HasFlag(options, "collapse-packages"),
            MeasurementMode = measurementMode
        };

        var results = new List<ScanBenchmarkResult>(runCount);
        for (var index = 0; index < runCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prepared = await PrepareBenchmarkAsync(
                fixtureKind,
                options,
                syntheticFiles,
                cancellationToken);
            var runner = new ScanBenchmarkRunner(prepared.Scanner);
            var result = await runner.RunAsync(
                prepared.RootPath,
                scanOptions,
                prepared.Fixture,
                cancelAfterProgress,
                cancellationToken);
            results.Add(result);
            await _output.WriteLineAsync(FormatSummary(index + 1, runCount, result));
        }

        await WriteJsonAsync<object>(results.Count == 1 ? results[0] : results, options);
        return results.Any(result => !result.IsCompleted && !result.IsCanceled)
            ? 1
            : 0;
    }

    private async Task<PreparedBenchmark> PrepareBenchmarkAsync(
        BenchmarkFixtureKind fixtureKind,
        IReadOnlyDictionary<string, string?> options,
        long syntheticFiles,
        CancellationToken cancellationToken)
    {
        return fixtureKind switch
        {
            BenchmarkFixtureKind.Existing => ExistingBenchmark(options),
            BenchmarkFixtureKind.Representative => await RepresentativeBenchmarkAsync(
                options,
                cancellationToken),
            BenchmarkFixtureKind.Synthetic => SyntheticBenchmark(syntheticFiles),
            _ => throw new ArgumentOutOfRangeException(nameof(fixtureKind))
        };
    }

    private static PreparedBenchmark ExistingBenchmark(
        IReadOnlyDictionary<string, string?> options)
    {
        var root = Required(options, "root");
        var fixture = new BenchmarkFixtureInfo(
            BenchmarkFixtureKind.Existing,
            root,
            "Existing filesystem tree",
            IsRealFileSystem: true,
            OrdinaryFileCount: null,
            SparseFileCount: null,
            HardlinkCount: null,
            SymbolicLinkCount: null,
            PackageCount: null,
            SyntheticFileCount: null,
            Limitations: []);
        return new PreparedBenchmark(root, fixture, new DiskScanner(new MacFileMetadataReader()));
    }

    private static async Task<PreparedBenchmark> RepresentativeBenchmarkAsync(
        IReadOnlyDictionary<string, string?> options,
        CancellationToken cancellationToken)
    {
        var root = Required(options, "root");
        var fixture = await RepresentativeFixtureGenerator.CreateAsync(
            root,
            cancellationToken);
        return new PreparedBenchmark(root, fixture, new DiskScanner(new MacFileMetadataReader()));
    }

    private static PreparedBenchmark SyntheticBenchmark(long syntheticFiles)
    {
        var scanner = new SyntheticDiskScanner(syntheticFiles);
        var fixture = new BenchmarkFixtureInfo(
            BenchmarkFixtureKind.Synthetic,
            scanner.RootPath,
            $"Synthetic scanner fixture with {syntheticFiles} files",
            IsRealFileSystem: false,
            OrdinaryFileCount: null,
            SparseFileCount: null,
            HardlinkCount: null,
            SymbolicLinkCount: null,
            PackageCount: null,
            SyntheticFileCount: syntheticFiles,
            Limitations: []);
        return new PreparedBenchmark(scanner.RootPath, fixture, scanner);
    }

    private async Task WriteJsonAsync<T>(
        T value,
        IReadOnlyDictionary<string, string?> options)
    {
        if (!options.TryGetValue("output", out var outputPath)
            || string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(
            stream,
            value,
            BenchmarkJson.Options);
        await stream.FlushAsync();
    }

    private static string FormatSummary(
        int runNumber,
        int runCount,
        ScanBenchmarkResult result)
    {
        var state = result.IsCompleted
            ? "completed"
            : result.IsCanceled
                ? "cancelled"
                : "incomplete";
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Run {runNumber}/{runCount}: {state}, {result.ObservedFileCount} files, {result.ObservedDirectoryCount} directories, {result.DurationMilliseconds:F2} ms, {result.EntriesPerSecond:F2} entries/s, {result.ErrorCount} errors");
    }

    private int UnknownCommand(string command)
    {
        _error.WriteLine($"Unknown command '{command}'.");
        WriteUsage(_error);
        return 2;
    }

    private static Dictionary<string, string?> ParseOptions(IEnumerable<string> args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        using var enumerator = args.GetEnumerator();
        while (enumerator.MoveNext())
        {
            var token = enumerator.Current;
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument '{token}'.");
            }

            var name = token[2..];
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Option name cannot be empty.");
            }

            if (IsBooleanOption(name))
            {
                options[name] = "true";
                continue;
            }

            if (!enumerator.MoveNext())
            {
                throw new ArgumentException($"Option '--{name}' requires a value.");
            }

            options[name] = enumerator.Current;
        }

        return options;
    }

    private static bool IsBooleanOption(string name) =>
        string.Equals(name, "include-hidden", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "follow-symlinks", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "collapse-packages", StringComparison.OrdinalIgnoreCase);

    private static bool HasFlag(
        IReadOnlyDictionary<string, string?> options,
        string name) =>
        options.ContainsKey(name);

    private static string Required(
        IReadOnlyDictionary<string, string?> options,
        string name)
    {
        if (!options.TryGetValue(name, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required option '--{name}'.");
        }

        return value;
    }

    private static string ValueOrDefault(
        IReadOnlyDictionary<string, string?> options,
        string name,
        string? fallback) =>
        options.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback ?? string.Empty;

    private static BenchmarkFixtureKind ParseFixtureKind(string value) =>
        value switch
        {
            "existing" => BenchmarkFixtureKind.Existing,
            "representative" => BenchmarkFixtureKind.Representative,
            "synthetic" => BenchmarkFixtureKind.Synthetic,
            _ => throw new ArgumentException(
                $"Unsupported fixture '{value}'. Use existing, representative, or synthetic.")
        };

    private static StorageMeasurementMode ParseMeasurementMode(string value) =>
        value switch
        {
            "logical" => StorageMeasurementMode.Logical,
            "allocated" => StorageMeasurementMode.Allocated,
            "shared-aware" => StorageMeasurementMode.SharedAwareAllocated,
            "shared-aware-allocated" => StorageMeasurementMode.SharedAwareAllocated,
            _ => throw new ArgumentException(
                $"Unsupported mode '{value}'. Use logical, allocated, or shared-aware.")
        };

    private static int ParsePositiveInt(string value, string name)
    {
        if (!int.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result)
            || result <= 0)
        {
            throw new ArgumentException($"Option '--{name}' must be a positive integer.");
        }

        return result;
    }

    private static int? TryParsePositiveInt(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : ParsePositiveInt(value, name);

    private static long ParsePositiveLong(string value, string name)
    {
        if (!long.TryParse(
                value,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var result)
            || result <= 0)
        {
            throw new ArgumentException($"Option '--{name}' must be a positive integer.");
        }

        return result;
    }

    private static bool IsHelp(string value) =>
        string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  MacStorageAtlas.Benchmarks fixture representative --root <path> [--output <file>]");
        writer.WriteLine("  MacStorageAtlas.Benchmarks run --fixture existing --root <path> [options]");
        writer.WriteLine("  MacStorageAtlas.Benchmarks run --fixture representative --root <path> [options]");
        writer.WriteLine("  MacStorageAtlas.Benchmarks run --fixture synthetic [--synthetic-files <count>] [options]");
        writer.WriteLine("Options:");
        writer.WriteLine("  --mode logical|allocated|shared-aware");
        writer.WriteLine("  --include-hidden");
        writer.WriteLine("  --follow-symlinks");
        writer.WriteLine("  --collapse-packages");
        writer.WriteLine("  --runs <count>");
        writer.WriteLine("  --cancel-after-progress <count>");
        writer.WriteLine("  --output <file>");
    }

    private static void WriteFixtureUsage(TextWriter writer)
    {
        writer.WriteLine("Usage:");
        writer.WriteLine("  MacStorageAtlas.Benchmarks fixture representative --root <path> [--output <file>]");
    }

    private sealed record PreparedBenchmark(
        string RootPath,
        BenchmarkFixtureInfo Fixture,
        IDiskScanner Scanner);
}
