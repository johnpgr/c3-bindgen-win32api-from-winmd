namespace WinmnDump.Model;

public sealed class ApiDatabase
{
    public Dictionary<string, ApiType> Types { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ApiFunction> Functions { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, ApiConstant> Constants { get; } = new(StringComparer.Ordinal);
}

public sealed class ApiType
{
    public required string OriginalName { get; init; }
    public required string Namespace { get; init; }
    public required ApiTypeKind Kind { get; set; }
    public string? AbiType { get; set; }
    public string? AliasTarget { get; set; }
    public List<ApiField> Fields { get; } = [];
    public ApiFunctionSignature? DelegateSignature { get; set; }
}

public enum ApiTypeKind
{
    Alias,
    Class,
    Delegate,
    Enum,
    Handle,
    Interface,
    Struct
}

public sealed class ApiField
{
    public required string OriginalName { get; init; }
    public required string Type { get; init; }
    public string? LiteralValue { get; init; }
}

public sealed class ApiFunction
{
    public required string OriginalName { get; init; }
    public required string Namespace { get; init; }
    public required string ReturnType { get; init; }
    public string? ImportName { get; init; }
    public string? ImportModule { get; init; }
    public List<ApiParameter> Parameters { get; } = [];
}

public sealed class ApiFunctionSignature
{
    public required string ReturnType { get; init; }
    public List<ApiParameter> Parameters { get; } = [];
}

public sealed class ApiParameter
{
    public required string OriginalName { get; init; }
    public required string Type { get; init; }
    public ParamDirection Direction { get; init; }
    public bool NonNull { get; init; }
    public bool Const { get; init; }
    public bool Optional { get; init; }
}

public enum ParamDirection
{
    None,
    In,
    Out,
    InOut
}

public sealed class ApiConstant
{
    public required string OriginalName { get; init; }
    public required string Namespace { get; init; }
    public required string Type { get; init; }
    public required string Value { get; init; }
}
