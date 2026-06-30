using System.Text.Json;

namespace WinmnDump.Generation;

public sealed class SubsetSpec
{
    public string Module { get; init; } = "win32";
    public List<string> Namespaces { get; init; } = [];
    public List<string> IncludeNamespaces { get; init; } = [];
    public List<string> IncludeImportModules { get; init; } = [];
    public List<string> IncludeConstantsMatching { get; init; } = [];
    public List<string> Functions { get; init; } = [];
    public List<string> Types { get; init; } = [];
    public List<string> Constants { get; init; } = [];
    public Dictionary<string, string> TypeNameOverrides { get; init; } = new(StringComparer.Ordinal);

    public static SubsetSpec Load(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<SubsetSpec>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new SubsetSpec();
    }
}

public sealed record SubsetResult(
    List<string> Types,
    List<string> Functions,
    List<string> Constants,
    List<string> Warnings);
