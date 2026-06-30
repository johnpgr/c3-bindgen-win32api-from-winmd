using System.Text;
using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class C3Emitter
{
    public string Emit(GeneratedBinding binding)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"module {binding.ModuleName};");
        sb.AppendLine();
        sb.AppendLine("// Generated from Windows.Win32.winmd. Original Win32 names are preserved with @cname/comments.");
        sb.AppendLine();
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

        EmitTypes(sb, binding);
        EmitConstants(sb, binding);
        EmitFunctions(sb, binding);

        return sb.ToString();
    }



    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
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
                    EmitAlias(sb, type);
                    break;
                case ApiTypeKind.Struct:
                    EmitStruct(sb, type);
                    break;
                case ApiTypeKind.Enum:
                    EmitEnumAlias(sb, type);
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

    private static void EmitEnumAlias(StringBuilder sb, GeneratedType type)
    {
        var valueField = type.Fields.FirstOrDefault(f => f.OriginalName == "value__");
        sb.AppendLine($"// Win32 original: {type.OriginalName}");
        sb.AppendLine($"alias {type.C3Name} = {valueField?.C3Type ?? "int"};");
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
