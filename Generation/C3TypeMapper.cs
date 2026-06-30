namespace WinmnDump.Generation;

public sealed class C3TypeMapper(C3NameProjector names)
{
    private readonly Dictionary<string, string> _primitiveMap = new(StringComparer.Ordinal)
    {
        ["void"] = "void",
        ["bool"] = "bool",
        ["CHAR"] = "char",
        ["I8"] = "ichar",
        ["U8"] = "char",
        ["I16"] = "short",
        ["U16"] = "ushort",
        ["I32"] = "int",
        ["U32"] = "uint",
        ["I64"] = "long",
        ["U64"] = "ulong",
        ["i8"] = "ichar",
        ["u8"] = "char",
        ["i16"] = "short",
        ["u16"] = "ushort",
        ["i32"] = "int",
        ["u32"] = "uint",
        ["i64"] = "long",
        ["u64"] = "ulong",
        ["f32"] = "float",
        ["f64"] = "double",
        ["isize"] = "iptr",
        ["usize"] = "uptr",
        ["char16"] = "ushort",
        ["Windows.Win32.Foundation.PSTR"] = "char*",
        ["Windows.Win32.Foundation.PWSTR"] = "ushort*",
        ["Windows.Win32.Foundation.PCSTR"] = "char*",
        ["Windows.Win32.Foundation.PCWSTR"] = "ushort*"
    };

    public string Map(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return "void";

        var normalized = StripModifiers(type.Trim());
        var arraySuffix = ExtractArraySuffix(ref normalized);
        var suffix = "";

        while (normalized.EndsWith("*", StringComparison.Ordinal) || normalized.EndsWith("&", StringComparison.Ordinal))
        {
            suffix += "*";
            normalized = normalized[..^1].TrimEnd();
        }

        if (_primitiveMap.TryGetValue(normalized, out var primitive))
            return primitive + suffix + arraySuffix;

        var bare = LastNameSegment(normalized);
        if (_primitiveMap.TryGetValue(bare, out var barePrimitive))
            return barePrimitive + suffix + arraySuffix;

        return names.TypeName(bare) + suffix + arraySuffix;
    }

    public static string BaseTypeName(string type)
    {
        var normalized = StripModifiers(type.Trim());
        _ = ExtractArraySuffix(ref normalized);

        while (normalized.EndsWith("*", StringComparison.Ordinal) || normalized.EndsWith("&", StringComparison.Ordinal))
            normalized = normalized[..^1].TrimEnd();

        return LastNameSegment(normalized);
    }

    public static bool IsPrimitiveBase(string type)
    {
        return BaseTypeName(type) is
            "void" or "bool" or
            "i8" or "u8" or
            "i16" or "u16" or
            "i32" or "u32" or
            "i64" or "u64" or
            "f32" or "f64" or
            "isize" or "usize" or
            "char16" or "string" or "object" or
            "CHAR" or "I8" or "U8" or
            "I16" or "U16" or
            "I32" or "U32" or
            "I64" or "U64";
    }

    public static bool IsPointerLike(string type) => type.Contains('*') || type.Contains('&') || type.Contains("[]");

    private static string ExtractArraySuffix(ref string type)
    {
        if (!type.EndsWith("]", StringComparison.Ordinal))
            return "";

        var start = type.LastIndexOf('[');
        if (start < 0)
            return "";

        var size = type[(start + 1)..^1];
        type = type[..start].TrimEnd();

        if (string.IsNullOrWhiteSpace(size))
            return "*";

        if (int.TryParse(size, out var count) && count > 0)
            return $"[{count}]";

        return "*";
    }

    private static string StripModifiers(string type)
    {
        var modIndex = type.IndexOf(" mod", StringComparison.Ordinal);
        return modIndex >= 0 ? type[..modIndex].TrimEnd() : type;
    }

    private static string LastNameSegment(string value)
    {
        var genericIndex = value.IndexOf('<', StringComparison.Ordinal);
        if (genericIndex >= 0)
            value = value[..genericIndex];

        var index = value.LastIndexOf('.');
        return index >= 0 ? value[(index + 1)..] : value;
    }
}
