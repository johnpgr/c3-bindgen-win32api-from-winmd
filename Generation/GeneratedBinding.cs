using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class GeneratedBinding
{
    public required string ModuleName { get; init; }
    public List<GeneratedLinkLibrary> LinkLibraries { get; } = [];
    public List<GeneratedType> Types { get; } = [];
    public List<GeneratedConstant> Constants { get; } = [];
    public List<GeneratedFunction> Functions { get; } = [];
    public List<GeneratedFunctionMacro> FunctionMacros { get; } = [];
    public List<string> Warnings { get; } = [];
}

public sealed class GeneratedLinkLibrary
{
    public required string Library { get; init; }
    public List<string> SourceImportModules { get; } = [];
}

public sealed class GeneratedType
{
    public required string OriginalName { get; init; }
    public required string C3Name { get; init; }
    public required string Namespace { get; init; }
    public required ApiTypeKind Kind { get; init; }
    public string? AbiType { get; init; }
    public string? AliasTarget { get; init; }
    public string? C3AliasTarget { get; init; }
    public required string C3DeclKind { get; init; }
    public required bool Emitted { get; init; }
    public List<GeneratedField> Fields { get; } = [];
    public string? DelegateReturnType { get; init; }
    public string? C3DelegateReturnType { get; init; }
    public List<GeneratedParameter> DelegateParameters { get; } = [];
}

public sealed class GeneratedField
{
    public required int Ordinal { get; init; }
    public required string OriginalName { get; init; }
    public required string C3Name { get; init; }
    public required string OriginalType { get; init; }
    public required string C3Type { get; init; }
    public string? LiteralValue { get; init; }
    public required bool Emitted { get; init; }
}

public sealed class GeneratedFunction
{
    public required string OriginalName { get; init; }
    public required string C3Name { get; init; }
    public required string Namespace { get; init; }
    public required string ReturnType { get; init; }
    public required string C3ReturnType { get; init; }
    public string? ImportName { get; init; }
    public string? ImportModule { get; init; }
    public string? LinkLibrary { get; init; }
    public required bool Emitted { get; init; }
    public List<GeneratedParameter> Parameters { get; } = [];
}

public sealed class GeneratedFunctionMacro
{
    public required string C3Name { get; init; }
    public required string Namespace { get; init; }
    public required GeneratedFunction AnsiFunction { get; init; }
    public required GeneratedFunction UnicodeFunction { get; init; }
}

public sealed class GeneratedParameter
{
    public required int Ordinal { get; init; }
    public required string OriginalName { get; init; }
    public required string C3Name { get; init; }
    public required string OriginalType { get; init; }
    public required string C3Type { get; init; }
    public required ParamDirection Direction { get; init; }
    public required bool NonNull { get; init; }
    public string? ContractAnnotation { get; init; }
    public bool Const { get; init; }
    public bool Optional { get; init; }
}

public sealed class GeneratedConstant
{
    public required string OriginalName { get; init; }
    public required string C3Name { get; init; }
    public required string Namespace { get; init; }
    public required string OriginalType { get; init; }
    public required string C3Type { get; init; }
    public required string Value { get; init; }
    public required string EmittedValue { get; init; }
}
