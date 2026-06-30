using System.Text.Json;
using WinmnDump.Generation;
using WinmnDump.Persistence;
using WinmnDump.Tests;
using WinmnDump.Winmd;

var options = CliOptions.Parse(args);

if (options.RunSelfTests)
{
    SelfTests.Run();
    return 0;
}

if (options.ShowHelp)
{
    Console.Error.WriteLine("""
        usage: WinmnDump --winmd <Windows.Win32.winmd> [--subset data/window-subset.json] [--out out/win32.c3i] [--dump-json raw.json] [--db out/bindgen-runs.sqlite]
               WinmnDump --self-test

        Legacy usage is also supported:
          WinmnDump <Windows.Win32.winmd> [namespace-prefix]
        """);
    return options.HasError ? 1 : 0;
}

if (options.WinmdPath is null)
{
    Console.Error.WriteLine("error: --winmd <path> is required");
    return 1;
}

var reader = new WinmdReader();
var api = reader.Read(options.WinmdPath, options.LegacyNamespacePrefix);

if (options.DumpJsonPath is not null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.DumpJsonPath)) ?? ".");
    File.WriteAllText(options.DumpJsonPath, JsonSerializer.Serialize(api, JsonOptions.Pretty));
}

if (options.LegacyMode && options.SubsetPath is null && options.OutPath is null)
{
    Console.WriteLine(JsonSerializer.Serialize(api.Types.Values, JsonOptions.Pretty));
    return 0;
}

var subsetPath = options.SubsetPath ?? Path.Combine("data", "window-subset.json");
var outPath = options.OutPath ?? Path.Combine("out", "win32.c3i");
var subsetJson = File.ReadAllText(subsetPath);
var spec = JsonSerializer.Deserialize<SubsetSpec>(subsetJson, JsonOptions.CamelCase) ??
    throw new InvalidOperationException($"failed to parse subset spec: {subsetPath}");
var resolved = new SubsetResolver().Resolve(api, spec);

var names = new C3NameProjector(spec.TypeNameOverrides);
var types = new C3TypeMapper(names);
var binding = new GeneratedBindingBuilder(names, types).Build(api, resolved, spec.Module);
var c3 = new C3Emitter().Emit(binding);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".");
File.WriteAllText(outPath, c3);

if (options.DbPath is not null)
{
    var runId = new BindgenDatabaseWriter().WriteRun(
        options.DbPath,
        new BindgenRunContext
        {
            WinmdPath = Path.GetFullPath(options.WinmdPath),
            SubsetPath = Path.GetFullPath(subsetPath),
            OutputPath = Path.GetFullPath(outPath),
            SubsetJson = subsetJson,
            GeneratorVersion = typeof(CliOptions).Assembly.GetName().Version?.ToString() ?? "1"
        },
        binding);

    Console.Error.WriteLine($"wrote bindgen database {options.DbPath} run {runId}");
}

Console.Error.WriteLine($"wrote {outPath}");
Console.Error.WriteLine($"resolved {resolved.Types.Count} types, {resolved.Functions.Count} functions, {resolved.Constants.Count} constants");
return 0;

internal sealed class CliOptions
{
    public string? WinmdPath { get; private init; }
    public string? SubsetPath { get; private init; }
    public string? OutPath { get; private init; }
    public string? DumpJsonPath { get; private init; }
    public string? DbPath { get; private init; }
    public string? LegacyNamespacePrefix { get; private init; }
    public bool LegacyMode { get; private init; }
    public bool ShowHelp { get; private init; }
    public bool HasError { get; private init; }
    public bool RunSelfTests { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        if (args.Length == 0)
            return new CliOptions { ShowHelp = true, HasError = true };

        if (args[0] is "-h" or "--help")
            return new CliOptions { ShowHelp = true };

        if (args[0] == "--self-test")
            return new CliOptions { RunSelfTests = true };

        if (!args[0].StartsWith("--", StringComparison.Ordinal))
        {
            return new CliOptions
            {
                WinmdPath = args[0],
                LegacyNamespacePrefix = args.Length >= 2 ? args[1] : null,
                LegacyMode = true
            };
        }

        string? winmd = null;
        string? subset = null;
        string? output = null;
        string? dumpJson = null;
        string? db = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"{arg} requires a value");
                return args[++i];
            }

            switch (arg)
            {
                case "--winmd":
                    winmd = Next();
                    break;
                case "--subset":
                    subset = Next();
                    break;
                case "--out":
                    output = Next();
                    break;
                case "--dump-json":
                    dumpJson = Next();
                    break;
                case "--db":
                    db = Next();
                    break;
                default:
                    throw new ArgumentException($"unknown argument: {arg}");
            }
        }

        return new CliOptions
        {
            WinmdPath = winmd,
            SubsetPath = subset,
            OutPath = output,
            DumpJsonPath = dumpJson,
            DbPath = db
        };
    }
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true
    };
}
