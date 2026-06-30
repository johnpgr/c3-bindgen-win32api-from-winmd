using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace WinmnDump.Winmd;

internal sealed class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
    public string GetPrimitiveType(PrimitiveTypeCode typeCode)
    {
        return typeCode switch
        {
            PrimitiveTypeCode.Void => "void",
            PrimitiveTypeCode.Boolean => "bool",
            PrimitiveTypeCode.Char => "u16",
            PrimitiveTypeCode.SByte => "i8",
            PrimitiveTypeCode.Byte => "u8",
            PrimitiveTypeCode.Int16 => "i16",
            PrimitiveTypeCode.UInt16 => "u16",
            PrimitiveTypeCode.Int32 => "i32",
            PrimitiveTypeCode.UInt32 => "u32",
            PrimitiveTypeCode.Int64 => "i64",
            PrimitiveTypeCode.UInt64 => "u64",
            PrimitiveTypeCode.Single => "f32",
            PrimitiveTypeCode.Double => "f64",
            PrimitiveTypeCode.IntPtr => "isize",
            PrimitiveTypeCode.UIntPtr => "usize",
            PrimitiveTypeCode.String => "string",
            PrimitiveTypeCode.Object => "object",
            _ => typeCode.ToString()
        };
    }

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeDefinition(handle);
        return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        var type = reader.GetTypeReference(handle);
        return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
    }

    public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
    {
        var spec = reader.GetTypeSpecification(handle);
        return spec.DecodeSignature(this, genericContext);
    }

    public string GetPointerType(string elementType) => $"{elementType}*";

    public string GetByReferenceType(string elementType) => $"{elementType}&";

    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    public string GetArrayType(string elementType, ArrayShape shape)
    {
        if (shape.Rank == 1 && shape.Sizes.Length == 1)
            return $"{elementType}[{shape.Sizes[0]}]";

        if (shape.Rank == 1)
            return $"{elementType}[]";

        return $"{elementType}[rank:{shape.Rank}]";
    }

    public string GetPinnedType(string elementType) => $"pinned {elementType}";

    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired)
    {
        return isRequired
            ? $"{unmodifiedType} modreq({modifier})"
            : $"{unmodifiedType} modopt({modifier})";
    }

    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
    {
        return $"{genericType}<{string.Join(", ", typeArguments)}>";
    }

    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        return $"fnptr({string.Join(", ", signature.ParameterTypes)}) -> {signature.ReturnType}";
    }

    private static string FullName(string ns, string name)
    {
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }
}
