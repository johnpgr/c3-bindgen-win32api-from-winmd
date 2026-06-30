using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using WinmnDump.Model;

namespace WinmnDump.Winmd;

public sealed class WinmdReader
{
    public ApiDatabase Read(string path, string? namespacePrefix = null)
    {
        using var fs = File.OpenRead(path);
        using var pe = new PEReader(fs);

        if (!pe.HasMetadata)
            throw new InvalidOperationException("File has no CLI metadata.");

        var reader = pe.GetMetadataReader();
        var provider = new SignatureTypeProvider();
        var database = new ApiDatabase();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            var ns = reader.GetString(type.Namespace);
            var name = reader.GetString(type.Name);

            if (namespacePrefix is not null && !ns.StartsWith(namespacePrefix, StringComparison.Ordinal))
                continue;

            if (name.StartsWith('<'))
                continue;

            var apiType = new ApiType
            {
                Namespace = ns,
                OriginalName = name,
                Kind = Classify(reader, type)
            };

            ReadFields(reader, provider, type, apiType, database);
            ReadMethods(reader, provider, type, apiType, database);
            PromoteSingleFieldAliases(apiType);

            database.Types[name] = apiType;
        }

        return database;
    }

    private static void ReadFields(
        MetadataReader reader,
        SignatureTypeProvider provider,
        TypeDefinition type,
        ApiType apiType,
        ApiDatabase database)
    {
        foreach (var fieldHandle in type.GetFields())
        {
            var field = reader.GetFieldDefinition(fieldHandle);
            var fieldName = reader.GetString(field.Name);
            var fieldType = TryDecodeField(reader, provider, field, apiType.OriginalName, fieldName);
            var literal = TryReadConstant(reader, field.GetDefaultValue());

            apiType.Fields.Add(new ApiField
            {
                OriginalName = fieldName,
                Type = fieldType,
                LiteralValue = literal
            });

            if (literal is not null && fieldName != "value__")
            {
                database.Constants[fieldName] = new ApiConstant
                {
                    Namespace = apiType.Namespace,
                    OriginalName = fieldName,
                    Type = apiType.Kind == ApiTypeKind.Enum ? apiType.OriginalName : fieldType,
                    Value = literal
                };
            }
        }
    }

    private static void ReadMethods(
        MetadataReader reader,
        SignatureTypeProvider provider,
        TypeDefinition type,
        ApiType apiType,
        ApiDatabase database)
    {
        foreach (var methodHandle in type.GetMethods())
        {
            var method = reader.GetMethodDefinition(methodHandle);
            var methodName = reader.GetString(method.Name);

            if (!TryDecodeMethod(reader, provider, method, apiType.OriginalName, methodName, out var sig))
                continue;

            var signature = new ApiFunctionSignature { ReturnType = sig.ReturnType };
            var paramDefs = method.GetParameters()
                .Select(reader.GetParameter)
                .Where(p => p.SequenceNumber > 0)
                .OrderBy(p => p.SequenceNumber)
                .ToArray();

            foreach (var p in paramDefs)
            {
                var index = p.SequenceNumber - 1;
                var paramName = reader.GetString(p.Name);
                var paramType = index >= 0 && index < sig.ParameterTypes.Length
                    ? sig.ParameterTypes[index]
                    : "<unparsed>";

                var isConst = HasCustomAttribute(reader, p.GetCustomAttributes(), "ConstAttribute", "Windows.Win32.Foundation.Metadata");
                var isOptional = (p.Attributes & ParameterAttributes.Optional) != 0 || HasCustomAttribute(reader, p.GetCustomAttributes(), "OptionalAttribute", "System.Runtime.InteropServices");

                signature.Parameters.Add(new ApiParameter
                {
                    OriginalName = string.IsNullOrWhiteSpace(paramName) ? $"param{index + 1}" : paramName,
                    Type = paramType,
                    Direction = DirectionFromAttributes(p.Attributes),
                    NonNull = !isOptional,
                    Const = isConst,
                    Optional = isOptional
                });
            }

            if (apiType.Kind == ApiTypeKind.Delegate && methodName == "Invoke")
                apiType.DelegateSignature = signature;

            if ((method.Attributes & MethodAttributes.PinvokeImpl) == 0)
                continue;

            var importName = methodName;
            string? importModule = null;

            try
            {
                var import = method.GetImport();
                var metadataImportName = reader.GetString(import.Name);
                if (!string.IsNullOrWhiteSpace(metadataImportName))
                    importName = metadataImportName;
                importModule = reader.GetString(reader.GetModuleReference(import.Module).Name);
            }
            catch (BadImageFormatException ex)
            {
                WarnUnparsed($"method import {apiType.Namespace}.{apiType.OriginalName}.{methodName}", ex);
            }

            var fn = new ApiFunction
            {
                Namespace = apiType.Namespace,
                OriginalName = importName,
                ReturnType = sig.ReturnType,
                ImportName = importName,
                ImportModule = importModule
            };

            foreach (var p in signature.Parameters)
                fn.Parameters.Add(p);

            database.Functions[importName] = fn;
        }
    }

    private static void PromoteSingleFieldAliases(ApiType apiType)
    {
        if (apiType.Kind != ApiTypeKind.Struct || apiType.Fields.Count != 1)
            return;

        var field = apiType.Fields[0];
        if (field.OriginalName == "Value")
        {
            apiType.Kind = IsHandleName(apiType.OriginalName) ? ApiTypeKind.Handle : ApiTypeKind.Alias;
            apiType.AliasTarget = field.Type;
        }
    }

    private static ApiTypeKind Classify(MetadataReader reader, TypeDefinition type)
    {
        var baseType = TryDescribeType(reader, type.BaseType);

        if (baseType == "System.Enum")
            return ApiTypeKind.Enum;

        if (baseType == "System.ValueType")
            return ApiTypeKind.Struct;

        if (baseType == "System.MulticastDelegate")
            return ApiTypeKind.Delegate;

        if ((type.Attributes & TypeAttributes.Interface) != 0)
            return ApiTypeKind.Interface;

        return ApiTypeKind.Class;
    }

    private static string TryDescribeType(MetadataReader reader, EntityHandle handle)
    {
        if (handle.IsNil)
            return "";

        try
        {
            return DescribeType(reader, handle);
        }
        catch (BadImageFormatException ex)
        {
            WarnUnparsed($"base type handle {handle.Kind}", ex);
            return "";
        }
    }

    private static string DescribeType(MetadataReader reader, EntityHandle handle)
    {
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => DescribeTypeDef(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => DescribeTypeRef(reader, (TypeReferenceHandle)handle),
            _ => ""
        };
    }

    private static string DescribeTypeDef(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var type = reader.GetTypeDefinition(handle);
        return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
    }

    private static string DescribeTypeRef(MetadataReader reader, TypeReferenceHandle handle)
    {
        var type = reader.GetTypeReference(handle);
        return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
    }

    private static string TryDecodeField(
        MetadataReader reader,
        SignatureTypeProvider provider,
        FieldDefinition field,
        string typeName,
        string fieldName)
    {
        try
        {
            return field.DecodeSignature(provider, null);
        }
        catch (BadImageFormatException ex)
        {
            WarnUnparsed($"field signature {typeName}.{fieldName}", ex);
            return "<unparsed>";
        }
    }

    private static bool TryDecodeMethod(
        MetadataReader reader,
        SignatureTypeProvider provider,
        MethodDefinition method,
        string typeName,
        string methodName,
        out MethodSignature<string> signature)
    {
        try
        {
            signature = method.DecodeSignature(provider, null);
            return true;
        }
        catch (BadImageFormatException ex)
        {
            WarnUnparsed($"method signature {typeName}.{methodName}", ex);
            signature = default;
            return false;
        }
    }

    private static string? TryReadConstant(MetadataReader reader, ConstantHandle handle)
    {
        if (handle.IsNil)
            return null;

        var constant = reader.GetConstant(handle);
        var bytes = reader.GetBlobBytes(constant.Value);

        return constant.TypeCode switch
        {
            ConstantTypeCode.Boolean => BitConverter.ToBoolean(bytes).ToString().ToLowerInvariant(),
            ConstantTypeCode.Char => BitConverter.ToUInt16(bytes).ToString(),
            ConstantTypeCode.SByte => ((sbyte)bytes[0]).ToString(),
            ConstantTypeCode.Byte => bytes[0].ToString(),
            ConstantTypeCode.Int16 => BitConverter.ToInt16(bytes).ToString(),
            ConstantTypeCode.UInt16 => BitConverter.ToUInt16(bytes).ToString(),
            ConstantTypeCode.Int32 => BitConverter.ToInt32(bytes).ToString(),
            ConstantTypeCode.UInt32 => BitConverter.ToUInt32(bytes).ToString(),
            ConstantTypeCode.Int64 => BitConverter.ToInt64(bytes).ToString(),
            ConstantTypeCode.UInt64 => BitConverter.ToUInt64(bytes).ToString(),
            ConstantTypeCode.Single => BitConverter.ToSingle(bytes).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ConstantTypeCode.Double => BitConverter.ToDouble(bytes).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ConstantTypeCode.String => System.Text.Encoding.Unicode.GetString(bytes).TrimEnd('\0'),
            _ => null
        };
    }

    private static ParamDirection DirectionFromAttributes(ParameterAttributes attributes)
    {
        var isIn = (attributes & ParameterAttributes.In) != 0;
        var isOut = (attributes & ParameterAttributes.Out) != 0;

        return (isIn, isOut) switch
        {
            (true, true) => ParamDirection.InOut,
            (true, false) => ParamDirection.In,
            (false, true) => ParamDirection.Out,
            _ => ParamDirection.None
        };
    }

    private static bool IsHandleName(string name)
    {
        if (name is "HRESULT" or "NTSTATUS")
            return false;

        return name.Length >= 2 && name[0] == 'H' && char.IsUpper(name[1]);
    }

    private static string FullName(string ns, string name)
    {
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private static bool HasCustomAttribute(MetadataReader reader, CustomAttributeHandleCollection customAttributes, string attributeName, string attributeNamespace)
    {
        foreach (var handle in customAttributes)
        {
            var customAttribute = reader.GetCustomAttribute(handle);
            var constructorHandle = customAttribute.Constructor;
            string? name = null;
            string? ns = null;

            if (constructorHandle.Kind == HandleKind.MethodDefinition)
            {
                var methodDef = reader.GetMethodDefinition((MethodDefinitionHandle)constructorHandle);
                var typeDefHandle = methodDef.GetDeclaringType();
                var typeDef = reader.GetTypeDefinition(typeDefHandle);
                name = reader.GetString(typeDef.Name);
                ns = reader.GetString(typeDef.Namespace);
            }
            else if (constructorHandle.Kind == HandleKind.MemberReference)
            {
                var memberRef = reader.GetMemberReference((MemberReferenceHandle)constructorHandle);
                var parentHandle = memberRef.Parent;
                if (parentHandle.Kind == HandleKind.TypeReference)
                {
                    var typeRef = reader.GetTypeReference((TypeReferenceHandle)parentHandle);
                    name = reader.GetString(typeRef.Name);
                    ns = reader.GetString(typeRef.Namespace);
                }
                else if (parentHandle.Kind == HandleKind.TypeDefinition)
                {
                    var typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)parentHandle);
                    name = reader.GetString(typeDef.Name);
                    ns = reader.GetString(typeDef.Namespace);
                }
            }

            if (name == attributeName && ns == attributeNamespace)
            {
                return true;
            }
        }
        return false;
    }

    private static void WarnUnparsed(string target, Exception exception)
    {
        Console.Error.WriteLine($"warning: could not parse {target}: {exception.Message}");
    }
}
