using System.Text;
using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class C3Emitter
{
    public string Emit(GeneratedBinding binding)
    {
        var sb = new StringBuilder();

        EmitHeader(sb, binding.ModuleName);
        EmitSharedDeclarations(sb, binding);
        EmitTypes(sb, binding);
        EmitConstants(sb, binding);
        EmitFunctions(sb, binding);
        EmitFunctionMacros(sb, binding);

        return sb.ToString();
    }

    public IReadOnlyDictionary<string, string> EmitFiles(GeneratedBinding binding)
    {
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var shared = new StringBuilder();
        EmitHeader(shared, binding.ModuleName);
        EmitSharedDeclarations(shared, binding);
        var sharedBinding = new GeneratedBinding { ModuleName = binding.ModuleName };
        sharedBinding.Types.AddRange(binding.Types.Where(t => string.IsNullOrWhiteSpace(t.Namespace)));
        sharedBinding.Constants.AddRange(binding.Constants.Where(c => string.IsNullOrWhiteSpace(c.Namespace)));
        sharedBinding.Functions.AddRange(binding.Functions.Where(f => string.IsNullOrWhiteSpace(f.Namespace)));
        sharedBinding.FunctionMacros.AddRange(binding.FunctionMacros.Where(m => string.IsNullOrWhiteSpace(m.Namespace)));
        EmitTypes(shared, sharedBinding);
        EmitConstants(shared, sharedBinding);
        EmitFunctions(shared, sharedBinding);
        EmitFunctionMacros(shared, sharedBinding);
        files["_shared.c3i"] = shared.ToString();

        foreach (var ns in binding.Types.Select(t => t.Namespace)
                     .Concat(binding.Constants.Select(c => c.Namespace))
                     .Concat(binding.Functions.Select(f => f.Namespace))
                     .Concat(binding.FunctionMacros.Select(m => m.Namespace))
                     .Where(ns => !string.IsNullOrWhiteSpace(ns))
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var namespaceBinding = new GeneratedBinding { ModuleName = binding.ModuleName };
            namespaceBinding.Types.AddRange(binding.Types.Where(t => t.Namespace == ns));
            namespaceBinding.Constants.AddRange(binding.Constants.Where(c => c.Namespace == ns));
            namespaceBinding.Functions.AddRange(binding.Functions.Where(f => f.Namespace == ns));
            namespaceBinding.FunctionMacros.AddRange(binding.FunctionMacros.Where(m => m.Namespace == ns));

            var sb = new StringBuilder();
            EmitHeader(sb, binding.ModuleName);
            sb.AppendLine($"// Win32 namespace: {ns}");
            sb.AppendLine();
            EmitTypes(sb, namespaceBinding);
            EmitConstants(sb, namespaceBinding);
            EmitFunctions(sb, namespaceBinding);
            EmitFunctionMacros(sb, namespaceBinding);

            var fileName = NamespaceFileName(ns);
            if (files.ContainsKey(fileName))
                throw new InvalidOperationException($"C3 output file name collision for namespace {ns}: {fileName}");

            files[fileName] = sb.ToString();
        }

        return files;
    }



    private static void EmitHeader(StringBuilder sb, string moduleName)
    {
        sb.AppendLine($"module {moduleName};");
        sb.AppendLine();
        sb.AppendLine("// Generated from Windows.Win32.winmd. Original Win32 names are preserved with @cname/comments.");
        sb.AppendLine();
    }

    private static void EmitSharedDeclarations(StringBuilder sb, GeneratedBinding binding)
    {
        sb.AppendLine("struct Guid");
        sb.AppendLine("{");
        sb.AppendLine("    uint data1;");
        sb.AppendLine("    ushort data2;");
        sb.AppendLine("    ushort data3;");
        sb.AppendLine("    char[8] data4;");
        sb.AppendLine("}");
        sb.AppendLine();

        foreach (var warning in binding.Warnings)
            sb.AppendLine($"// warning: {warning}");

        if (binding.Warnings.Count > 0)
            sb.AppendLine();
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string NamespaceFileName(string ns)
    {
        const string prefix = "Windows.Win32.";
        if (ns.StartsWith(prefix, StringComparison.Ordinal))
            ns = ns[prefix.Length..];

        var parts = ns.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ToSnakeFilePart)
            .Where(part => part.Length > 0)
            .ToList();

        var fileName = parts.Count == 0 ? "global" : string.Join("_", parts);
        return fileName + ".c3i";
    }

    private static string ToSnakeFilePart(string value)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (!char.IsAsciiLetterOrDigit(current))
            {
                if (sb.Length > 0 && sb[^1] != '_')
                    sb.Append('_');
                continue;
            }

            if (i > 0 &&
                char.IsAsciiLetterUpper(current) &&
                (char.IsAsciiLetterLower(value[i - 1]) ||
                 (i + 1 < value.Length && char.IsAsciiLetterLower(value[i + 1]) && char.IsAsciiLetterUpper(value[i - 1]))))
            {
                if (sb.Length > 0 && sb[^1] != '_')
                    sb.Append('_');
            }

            sb.Append(char.ToLowerInvariant(current));
        }

        return sb.ToString().Trim('_');
    }

    private static void EmitTypes(StringBuilder sb, GeneratedBinding binding)
    {
        foreach (var type in binding.Types)
        {
            if (!type.Emitted)
                continue;

            switch (type.Kind)
            {
                case ApiTypeKind.Handle:
                    EmitHandle(sb, type);
                    break;
                case ApiTypeKind.Alias:
                case ApiTypeKind.Interface:
                case ApiTypeKind.Class:
                    EmitAlias(sb, type);
                    break;
                case ApiTypeKind.Struct:
                    EmitStruct(sb, type);
                    break;
                case ApiTypeKind.Enum:
                    EmitEnum(sb, type);
                    break;
                case ApiTypeKind.Delegate:
                    EmitDelegate(sb, type);
                    break;
            }

            sb.AppendLine();
        }
    }

    private static void EmitConstants(StringBuilder sb, GeneratedBinding binding)
    {
        foreach (var constant in binding.Constants)
        {
            sb.AppendLine($"// Win32 original: {constant.OriginalName}");
            sb.AppendLine($"const {constant.C3Type} {constant.C3Name} = {constant.EmittedValue};");
            sb.AppendLine();
        }
    }

    private static void EmitFunctions(StringBuilder sb, GeneratedBinding binding)
    {
        foreach (var fn in binding.Functions)
        {
            if (!fn.Emitted)
                continue;

            EmitFunction(sb, fn);
            sb.AppendLine();
        }
    }

    private static void EmitFunctionMacros(StringBuilder sb, GeneratedBinding binding)
    {
        foreach (var macro in binding.FunctionMacros)
        {
            EmitFunctionMacroVariant(sb, macro, macro.AnsiFunction, "!$feature(WIN32_UNICODE)");
            sb.AppendLine();
            EmitFunctionMacroVariant(sb, macro, macro.UnicodeFunction, "$feature(WIN32_UNICODE)");
            sb.AppendLine();
        }
    }

    private static void EmitHandle(StringBuilder sb, GeneratedType type)
    {
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {type.C3Name} = void*;");
    }

    private static void EmitAlias(StringBuilder sb, GeneratedType type)
    {
        var target = type.C3AliasTarget ?? "void*";

        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {type.C3Name} = {target};");
    }

    private static void EmitEnum(StringBuilder sb, GeneratedType type)
    {
        var valueField = type.Fields.FirstOrDefault(f => f.OriginalName == "value__");
        var underlyingType = valueField?.C3Type ?? "int";

        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"constdef {type.C3Name} : {underlyingType}");
        sb.AppendLine("{");

        var members = type.Fields.Where(f => f.OriginalName != "value__").ToList();
        for (int i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var comma = i == members.Count - 1 ? "" : ",";
            sb.AppendLine($"    {member.C3Name} = {member.LiteralValue}{comma}");
        }

        sb.AppendLine("}");
    }

    private static void EmitStruct(StringBuilder sb, GeneratedType type)
    {
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"struct {type.C3Name}");
        sb.AppendLine("{");
        var emittedFields = 0;

        foreach (var field in type.Fields.Where(field => field.Emitted))
        {
            sb.AppendLine($"    {field.C3Type} {field.C3Name};");
            emittedFields++;
        }

        if (emittedFields == 0)
            sb.AppendLine("    char unused;");

        sb.AppendLine("}");
    }

    private static void EmitDelegate(StringBuilder sb, GeneratedType type)
    {
        if (type.C3DelegateReturnType is null)
            return;

        var parameters = type.DelegateParameters
            .Select(p => $"{p.C3Type} {p.C3Name}");

        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {type.C3Name} = fn {type.C3DelegateReturnType}({string.Join(", ", parameters)});");
    }

    private static void EmitFunction(StringBuilder sb, GeneratedFunction fn)
    {
        var contract = EmitContract(fn.Parameters);
        if (!string.IsNullOrWhiteSpace(contract))
            sb.Append(contract);

        var parameters = fn.Parameters.Select(p => $"{p.C3Type} {p.C3Name}");

        sb.AppendLine($"extern fn {fn.C3ReturnType} {fn.C3Name}({string.Join(", ", parameters)})");
        sb.Append($"    @cname(\"{fn.OriginalName}\")");
        if (fn.LinkLibrary is not null)
        {
            sb.Append($" @link(\"{Escape(fn.LinkLibrary)}\")");
        }
        sb.AppendLine(";");
    }

    private static void EmitFunctionMacroVariant(
        StringBuilder sb,
        GeneratedFunctionMacro macro,
        GeneratedFunction target,
        string condition)
    {
        var contract = EmitContract(target.Parameters);
        if (!string.IsNullOrWhiteSpace(contract))
            sb.Append(contract);

        var parameters = target.Parameters.Select(p => $"{p.C3Type} {p.C3Name}");
        var arguments = string.Join(", ", target.Parameters.Select(p => p.C3Name));

        sb.AppendLine($"macro {target.C3ReturnType} {macro.C3Name}({string.Join(", ", parameters)})");
        sb.AppendLine($"@if({condition})");
        sb.AppendLine("{");
        if (target.C3ReturnType == "void")
            sb.AppendLine($"    {target.C3Name}({arguments});");
        else
            sb.AppendLine($"    return {target.C3Name}({arguments});");
        sb.AppendLine("}");
    }

    private static string EmitContract(List<GeneratedParameter> parameters)
    {
        var lines = new List<string>();

        foreach (var p in parameters)
        {
            if (p.ContractAnnotation is not null)
                lines.Add($" @param {p.ContractAnnotation} {p.C3Name}");
        }

        return lines.Count == 0 ? "" : "<*\n" + string.Join("\n", lines) + "\n*>\n";
    }
}
