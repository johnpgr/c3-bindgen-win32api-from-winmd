using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class GeneratedBindingBuilder(C3NameProjector names, C3TypeMapper types)
{
    public GeneratedBinding Build(ApiDatabase api, SubsetResult subset, string moduleName)
    {
        var binding = new GeneratedBinding { ModuleName = moduleName };
        binding.Warnings.AddRange(subset.Warnings);
        AddLinkLibraries(binding, api, subset);
        AddTypes(binding, api, subset);
        AddConstants(binding, api, subset);
        AddFunctions(binding, api, subset);
        AddFunctionMacros(binding);
        return binding;
    }

    private static void AddLinkLibraries(GeneratedBinding binding, ApiDatabase api, SubsetResult subset)
    {
        var modulesByLibrary = new Dictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var functionName in subset.Functions)
        {
            if (!api.Functions.TryGetValue(functionName, out var fn) || string.IsNullOrWhiteSpace(fn.ImportModule))
                continue;

            var library = NormalizeLinkLibrary(fn.ImportModule);
            if (string.IsNullOrWhiteSpace(library) || IsApiSetLibrary(library))
                continue;

            if (!modulesByLibrary.TryGetValue(library, out var modules))
            {
                modules = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                modulesByLibrary[library] = modules;
            }

            modules.Add(fn.ImportModule);
        }

        foreach (var (library, sourceModules) in modulesByLibrary.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var link = new GeneratedLinkLibrary { Library = library };
            link.SourceImportModules.AddRange(sourceModules);
            binding.LinkLibraries.Add(link);
        }
    }

    private void AddTypes(GeneratedBinding binding, ApiDatabase api, SubsetResult subset)
    {
        foreach (var typeName in subset.Types)
        {
            if (!api.Types.TryGetValue(typeName, out var type))
                continue;

            var c3DeclKind = C3DeclKind(type);
            var emitted = !IsAnonymousHelperType(type.OriginalName) && c3DeclKind != "unsupported";
            var generated = new GeneratedType
            {
                OriginalName = type.OriginalName,
                C3Name = names.TypeName(type.OriginalName),
                Namespace = type.Namespace,
                Kind = type.Kind,
                AbiType = type.AbiType,
                AliasTarget = type.AliasTarget,
                C3AliasTarget = type.Kind == ApiTypeKind.Alias
                    ? types.Map(type.AliasTarget ?? type.AbiType ?? "void*")
                    : null,
                C3DeclKind = c3DeclKind,
                Emitted = emitted,
                DelegateReturnType = type.DelegateSignature?.ReturnType,
                C3DelegateReturnType = type.DelegateSignature is null ? null : types.Map(type.DelegateSignature.ReturnType)
            };

            var isEnum = type.Kind == ApiTypeKind.Enum;
            for (var i = 0; i < type.Fields.Count; i++)
            {
                var field = type.Fields[i];
                var fieldEmitted = field.OriginalName != "value__" &&
                    !IsAnonymousHelperType(C3TypeMapper.BaseTypeName(field.Type));

                generated.Fields.Add(new GeneratedField
                {
                    Ordinal = i,
                    OriginalName = field.OriginalName,
                    C3Name = isEnum
                        ? names.ConstantName(field.OriginalName)
                        : names.FieldName(field.OriginalName),
                    OriginalType = field.Type,
                    C3Type = types.Map(field.Type),
                    LiteralValue = field.LiteralValue,
                    Emitted = fieldEmitted
                });
            }

            if (type.DelegateSignature is not null)
            {
                for (var i = 0; i < type.DelegateSignature.Parameters.Count; i++)
                {
                    var parameter = type.DelegateSignature.Parameters[i];
                    generated.DelegateParameters.Add(new GeneratedParameter
                    {
                        Ordinal = i,
                        OriginalName = parameter.OriginalName,
                        C3Name = names.ParameterName(parameter.OriginalName),
                        OriginalType = parameter.Type,
                        C3Type = types.Map(parameter.Type),
                        Direction = parameter.Direction,
                        NonNull = parameter.NonNull,
                        Const = parameter.Const,
                        Optional = parameter.Optional,
                        ContractAnnotation = ParamAnnotation(parameter)
                    });
                }
            }

            binding.Types.Add(generated);
        }
    }

    private void AddConstants(GeneratedBinding binding, ApiDatabase api, SubsetResult subset)
    {
        foreach (var constantName in subset.Constants)
        {
            if (!api.Constants.TryGetValue(constantName, out var constant))
                continue;

            if (api.Types.TryGetValue(constant.Type, out var enumType) && enumType.Kind == ApiTypeKind.Enum && subset.Types.Contains(constant.Type))
                continue;

            var c3Type = types.Map(constant.Type);
            binding.Constants.Add(new GeneratedConstant
            {
                OriginalName = constant.OriginalName,
                C3Name = names.ConstantName(constant.OriginalName),
                Namespace = constant.Namespace,
                OriginalType = constant.Type,
                C3Type = c3Type,
                Value = constant.Value,
                EmittedValue = FormatConstantValue(c3Type, constant.Value)
            });
        }
    }

    private void AddFunctions(GeneratedBinding binding, ApiDatabase api, SubsetResult subset)
    {
        var c3FunctionNames = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var functionName in subset.Functions)
        {
            if (!api.Functions.TryGetValue(functionName, out var fn))
                continue;

            string? linkLibrary = null;
            if (!string.IsNullOrWhiteSpace(fn.ImportModule))
            {
                var library = NormalizeLinkLibrary(fn.ImportModule);
                if (!string.IsNullOrWhiteSpace(library) && !IsApiSetLibrary(library))
                {
                    linkLibrary = library;
                }
            }

            var c3Name = names.FunctionName(fn.OriginalName);
            if (c3FunctionNames.TryGetValue(c3Name, out var otherOriginal) && otherOriginal != fn.OriginalName)
            {
                throw new InvalidOperationException(
                    $"C3 function name collision: {fn.OriginalName} and {otherOriginal} both project to {c3Name}");
            }

            c3FunctionNames[c3Name] = fn.OriginalName;

            var generated = new GeneratedFunction
            {
                OriginalName = fn.OriginalName,
                C3Name = c3Name,
                Namespace = fn.Namespace,
                ReturnType = fn.ReturnType,
                C3ReturnType = types.Map(fn.ReturnType),
                ImportName = fn.ImportName,
                ImportModule = fn.ImportModule,
                LinkLibrary = linkLibrary,
                Emitted = true
            };

            for (var i = 0; i < fn.Parameters.Count; i++)
            {
                var parameter = fn.Parameters[i];
                generated.Parameters.Add(new GeneratedParameter
                {
                    Ordinal = i,
                    OriginalName = parameter.OriginalName,
                    C3Name = names.ParameterName(parameter.OriginalName),
                    OriginalType = parameter.Type,
                    C3Type = types.Map(parameter.Type),
                    Direction = parameter.Direction,
                    NonNull = parameter.NonNull,
                    Const = parameter.Const,
                    Optional = parameter.Optional,
                    ContractAnnotation = ParamAnnotation(parameter)
                });
            }

            binding.Functions.Add(generated);
        }
    }

    private void AddFunctionMacros(GeneratedBinding binding)
    {
        var realFunctionNames = binding.Functions
            .Select(fn => fn.C3Name)
            .ToHashSet(StringComparer.Ordinal);
        var macrosByName = new Dictionary<string, string>(StringComparer.Ordinal);
        var variantsByBase = new Dictionary<(string Namespace, string BaseName), (GeneratedFunction? Ansi, GeneratedFunction? Unicode)>();

        foreach (var fn in binding.Functions.Where(fn => fn.Emitted))
        {
            if (fn.OriginalName.Length <= 1 || fn.OriginalName[^1] is not ('A' or 'W'))
                continue;

            var key = (fn.Namespace, fn.OriginalName[..^1]);
            variantsByBase.TryGetValue(key, out var variants);
            if (fn.OriginalName[^1] == 'A')
                variants.Ansi = fn;
            else
                variants.Unicode = fn;
            variantsByBase[key] = variants;
        }

        foreach (var ((ns, baseName), variants) in variantsByBase.OrderBy(pair => pair.Key.Namespace, StringComparer.Ordinal)
                     .ThenBy(pair => pair.Key.BaseName, StringComparer.Ordinal))
        {
            if (variants.Ansi is null || variants.Unicode is null)
                continue;

            if (!CanGenerateNeutralMacro(variants.Ansi, variants.Unicode))
                continue;

            var neutralName = names.FunctionName(baseName);
            if (realFunctionNames.Contains(neutralName))
                continue;

            if (macrosByName.TryGetValue(neutralName, out var otherBase) && otherBase != $"{ns}.{baseName}")
                continue;

            macrosByName[neutralName] = $"{ns}.{baseName}";
            binding.FunctionMacros.Add(new GeneratedFunctionMacro
            {
                C3Name = neutralName,
                Namespace = ns,
                AnsiFunction = variants.Ansi,
                UnicodeFunction = variants.Unicode
            });
        }
    }

    private static bool CanGenerateNeutralMacro(GeneratedFunction ansi, GeneratedFunction unicode)
    {
        if (ansi.C3ReturnType != unicode.C3ReturnType ||
            ansi.Parameters.Count != unicode.Parameters.Count)
        {
            return false;
        }

        for (var i = 0; i < ansi.Parameters.Count; i++)
        {
            if (!CompatibleNeutralMacroType(ansi.Parameters[i].C3Type, unicode.Parameters[i].C3Type))
                return false;
        }

        return true;
    }

    private static bool CompatibleNeutralMacroType(string ansiType, string unicodeType)
    {
        if (ansiType == unicodeType)
            return true;

        if (ansiType == "char*" && unicodeType == "ushort*")
            return true;

        return TryStripSuffixType(ansiType, 'A', out var ansiBase) &&
            TryStripSuffixType(unicodeType, 'W', out var unicodeBase) &&
            ansiBase == unicodeBase;
    }

    private static bool TryStripSuffixType(string type, char suffix, out string stripped)
    {
        var pointerSuffix = "";
        while (type.EndsWith('*'))
        {
            pointerSuffix += "*";
            type = type[..^1];
        }

        if (type.Length > 1 && type[^1] == suffix)
        {
            stripped = type[..^1] + pointerSuffix;
            return true;
        }

        stripped = "";
        return false;
    }

    private static string C3DeclKind(ApiType type)
    {
        return type.Kind switch
        {
            ApiTypeKind.Handle => "alias",
            ApiTypeKind.Alias => "alias",
            ApiTypeKind.Struct => "struct",
            ApiTypeKind.Enum => "enum",
            ApiTypeKind.Delegate => type.DelegateSignature is null ? "unsupported" : "alias",
            ApiTypeKind.Interface => "alias",
            ApiTypeKind.Class => "alias",
            _ => "unsupported"
        };
    }

    internal static bool IsAnonymousHelperType(string originalName)
    {
        return originalName.StartsWith("_", StringComparison.Ordinal) &&
            originalName.Contains("Anonymous", StringComparison.Ordinal);
    }

    internal static string FormatConstantValue(string c3Type, string value)
    {
        if (c3Type.Contains('*', StringComparison.Ordinal) && IsIntegerLiteral(value))
            return $"({c3Type}){value}";

        return value;
    }

    internal static string? ParamAnnotation(ApiParameter p)
    {
        if (!C3TypeMapper.IsPointerLike(p.Type))
            return null;

        var direction = p.Direction switch
        {
            ParamDirection.In => "in",
            ParamDirection.Out => "out",
            ParamDirection.InOut => "inout",
            _ => p.Const ? "in" : ""
        };

        if (direction == "")
            return null;

        return p.NonNull ? $"[&{direction}]" : $"[{direction}]";
    }

    private static string NormalizeLinkLibrary(string module)
    {
        var name = Path.GetFileNameWithoutExtension(module.Trim());
        return name.ToLowerInvariant();
    }

    private static bool IsApiSetLibrary(string library)
    {
        return library.StartsWith("api-ms-", StringComparison.OrdinalIgnoreCase) ||
            library.StartsWith("ext-ms-", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsIntegerLiteral(string value)
    {
        if (value.Length == 0)
            return false;

        var start = value[0] == '-' || value[0] == '+' ? 1 : 0;
        return start < value.Length && value[start..].All(char.IsDigit);
    }
}
