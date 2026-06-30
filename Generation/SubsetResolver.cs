using WinmnDump.Model;

namespace WinmnDump.Generation;

public sealed class SubsetResolver
{
    public SubsetResult Resolve(ApiDatabase api, SubsetSpec spec)
    {
        var neededTypes = new HashSet<string>(spec.Types, StringComparer.Ordinal);
        var neededFunctions = new HashSet<string>(spec.Functions, StringComparer.Ordinal);
        var neededConstants = new HashSet<string>(spec.Constants, StringComparer.Ordinal);
        var warnings = new List<string>();
        var queue = new Queue<(string Kind, string Name)>();

        SeedFromNamespaces(api, spec, neededTypes, neededFunctions, neededConstants);
        SeedFromImportModules(api, spec, neededFunctions);
        SeedConstantsFromPatterns(api, spec, neededConstants);

        foreach (var fn in neededFunctions)
            queue.Enqueue(("function", fn));

        foreach (var type in neededTypes)
            queue.Enqueue(("type", type));

        foreach (var constant in neededConstants)
            queue.Enqueue(("constant", constant));

        while (queue.Count > 0)
        {
            var (kind, name) = queue.Dequeue();

            if (kind == "function")
            {
                if (!api.Functions.TryGetValue(name, out var fn))
                {
                    warnings.Add($"missing function: {name}");
                    continue;
                }

                AddType(fn.ReturnType);
                foreach (var p in fn.Parameters)
                    AddType(p.Type);
            }
            else if (kind == "type")
            {
                if (!api.Types.TryGetValue(name, out var type))
                {
                    warnings.Add($"missing type: {name}");
                    continue;
                }

                if (type.AliasTarget is not null)
                    AddType(type.AliasTarget);

                foreach (var field in type.Fields)
                    AddType(field.Type);

                if (type.DelegateSignature is not null)
                {
                    AddType(type.DelegateSignature.ReturnType);
                    foreach (var p in type.DelegateSignature.Parameters)
                        AddType(p.Type);
                }
            }
            else if (kind == "constant" && !api.Constants.ContainsKey(name))
            {
                warnings.Add($"missing constant: {name}");
            }
            else if (kind == "constant")
            {
                AddType(api.Constants[name].Type);
            }
        }

        return new SubsetResult(
            SortByNamespaceThenName(api.Types, neededTypes),
            SortByNamespaceThenName(api.Functions, neededFunctions),
            SortByNamespaceThenName(api.Constants, neededConstants),
            warnings);

        void AddType(string rawType)
        {
            if (C3TypeMapper.IsPrimitiveBase(rawType))
                return;

            var baseType = C3TypeMapper.BaseTypeName(rawType);
            if (!api.Types.ContainsKey(baseType))
                return;

            if (neededTypes.Add(baseType))
                queue.Enqueue(("type", baseType));
        }
    }

    private static void SeedFromNamespaces(
        ApiDatabase api,
        SubsetSpec spec,
        HashSet<string> neededTypes,
        HashSet<string> neededFunctions,
        HashSet<string> neededConstants)
    {
        if (spec.IncludeNamespaces.Count == 0)
            return;

        foreach (var (name, function) in api.Functions)
        {
            if (MatchesAnyNamespace(function.Namespace, spec.IncludeNamespaces))
                neededFunctions.Add(name);
        }
    }

    private static void SeedFromImportModules(
        ApiDatabase api,
        SubsetSpec spec,
        HashSet<string> neededFunctions)
    {
        if (spec.IncludeImportModules.Count == 0)
            return;

        var modules = spec.IncludeImportModules
            .Select(NormalizeModuleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, function) in api.Functions)
        {
            if (function.ImportModule is not null && modules.Contains(NormalizeModuleName(function.ImportModule)))
                neededFunctions.Add(name);
        }
    }

    private static void SeedConstantsFromPatterns(
        ApiDatabase api,
        SubsetSpec spec,
        HashSet<string> neededConstants)
    {
        if (spec.IncludeConstantsMatching.Count == 0)
            return;

        foreach (var constantName in api.Constants.Keys)
        {
            if (spec.IncludeConstantsMatching.Any(pattern => WildcardMatch(constantName, pattern)))
                neededConstants.Add(constantName);
        }
    }

    private static bool MatchesAnyNamespace(string ns, List<string> namespaceSpecs)
    {
        return namespaceSpecs.Any(spec =>
            ns.Equals(spec, StringComparison.Ordinal) ||
            ns.StartsWith(spec + ".", StringComparison.Ordinal));
    }

    private static string NormalizeModuleName(string module)
    {
        return Path.GetFileNameWithoutExtension(module.Trim());
    }

    private static bool WildcardMatch(string value, string pattern)
    {
        var valueIndex = 0;
        var patternIndex = 0;
        var starIndex = -1;
        var matchIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                valueIndex++;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                matchIndex = valueIndex;
            }
            else if (starIndex != -1)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++matchIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == pattern.Length;
    }

    private static List<string> SortByNamespaceThenName<T>(Dictionary<string, T> source, HashSet<string> names)
    {
        return names
            .OrderBy(name => source.TryGetValue(name, out var value) ? NamespaceOf(value) : "")
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string NamespaceOf<T>(T value)
    {
        return value switch
        {
            ApiType type => type.Namespace,
            ApiFunction function => function.Namespace,
            ApiConstant constant => constant.Namespace,
            _ => ""
        };
    }
}
