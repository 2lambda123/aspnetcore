// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Dynamic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Any;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection.Metadata;
using System.Runtime.Serialization.DataContracts;
using Microsoft.OpenApi.Writers;
using Microsoft.OpenApi;

namespace Microsoft.AspNetCore.OpenApi.Tests;

public class SchemaGeneratorTests
{
    private static readonly Dictionary<Type, Func<OpenApiSchema>> _primitiveTypeToOpenApiSchema =
        new()
        {
            [typeof(bool)] = () => new OpenApiSchema { Type = "boolean" },
            [typeof(byte)] = () => new OpenApiSchema { Type = "string", Format = "byte" },
            [typeof(int)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(uint)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(ushort)] = () => new OpenApiSchema { Type = "integer", Format = "int32" },
            [typeof(long)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
            [typeof(ulong)] = () => new OpenApiSchema { Type = "integer", Format = "int64" },
            [typeof(float)] = () => new OpenApiSchema { Type = "number", Format = "float" },
            [typeof(double)] = () => new OpenApiSchema { Type = "number", Format = "double" },
            [typeof(decimal)] = () => new OpenApiSchema { Type = "number", Format = "double" },
            [typeof(DateTime)] = () => new OpenApiSchema { Type = "string", Format = "date-time" },
            [typeof(DateTimeOffset)] = () => new OpenApiSchema { Type = "string", Format = "date-time" },
            [typeof(Guid)] = () => new OpenApiSchema { Type = "string", Format = "uuid" },
            [typeof(char)] = () => new OpenApiSchema { Type = "string" },
            [typeof(bool?)] = () => new OpenApiSchema { Type = "boolean", Nullable = true },
            [typeof(byte?)] = () => new OpenApiSchema { Type = "string", Format = "byte", Nullable = true },
            [typeof(int?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(uint?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(ushort?)] = () => new OpenApiSchema { Type = "integer", Format = "int32", Nullable = true },
            [typeof(long?)] = () => new OpenApiSchema { Type = "integer", Format = "int64", Nullable = true },
            [typeof(ulong?)] = () => new OpenApiSchema { Type = "integer", Format = "int64", Nullable = true },
            [typeof(float?)] = () => new OpenApiSchema { Type = "number", Format = "float", Nullable = true },
            [typeof(double?)] = () => new OpenApiSchema { Type = "number", Format = "double", Nullable = true },
            [typeof(decimal?)] = () => new OpenApiSchema { Type = "number", Format = "double", Nullable = true },
            [typeof(DateTime?)] = () => new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
            [typeof(DateTimeOffset?)] = () =>
                new OpenApiSchema { Type = "string", Format = "date-time", Nullable = true },
            [typeof(Guid?)] = () => new OpenApiSchema { Type = "string", Format = "uuid", Nullable = true },
            [typeof(char?)] = () => new OpenApiSchema { Type = "string", Nullable = true },
            // Uri is treated as simple string.
            [typeof(Uri)] = () => new OpenApiSchema { Type = "string" },
            [typeof(string)] = () => new OpenApiSchema { Type = "string" },
            [typeof(object)] = () => new OpenApiSchema { Type = "object" }
        };

    [Theory]
    [InlineData(typeof(bool), "boolean", null)]
    [InlineData(typeof(byte), "string", "byte")]
    [InlineData(typeof(sbyte), "integer", "int32")]
    [InlineData(typeof(short), "integer", "int32")]
    [InlineData(typeof(ushort), "integer", "int32")]
    [InlineData(typeof(int), "integer", "int32")]
    [InlineData(typeof(uint), "integer", "int32")]
    [InlineData(typeof(long), "integer", "int64")]
    [InlineData(typeof(ulong), "integer", "int64")]
    [InlineData(typeof(float), "number", "float")]
    [InlineData(typeof(double), "number", "double")]
    [InlineData(typeof(decimal), "number", "double")]
    [InlineData(typeof(string), "string", null)]
    [InlineData(typeof(char), "string", null)]
    [InlineData(typeof(byte[]), "string", "byte")]
    [InlineData(typeof(DateTime), "string", "date-time")]
    [InlineData(typeof(DateTimeOffset), "string", "date-time")]
    [InlineData(typeof(Guid), "string", "uuid")]
    [InlineData(typeof(Uri), "string", "uri")]
    [InlineData(typeof(bool?), "boolean", null)]
    [InlineData(typeof(int?), "integer", "int32")]
    [InlineData(typeof(DateTime?), "string", "date-time")]
    [InlineData(typeof(Guid?), "string", "uuid")]
    public void GenerateSchemaForType_PrimitiveOrNullablePrimitive(Type type, string expectedType, string expectedFormat)
    {
        var schema = GetSchemaFromType(type);
        Assert.Equal(expectedType, schema.Type);
        Assert.Equal(expectedFormat, schema.Format);
    }

    [Theory]
    [InlineData(typeof(IntEnum), "integer", "int32")]
    [InlineData(typeof(LongEnum), "integer", "int64")]
    [InlineData(typeof(IntEnum?), "integer", "int32")]
    [InlineData(typeof(LongEnum?), "integer", "int64")]
    public void GenerateSchemaForType_ReferencedEnumSchema(Type type, string expectedType, string expectedFormat)
    {
        var schema = GetSchemaFromType(type);
        Assert.Equal(expectedType, schema.Type);
        Assert.Equal(expectedFormat, schema.Format);
    }

    [Theory]
    [InlineData(typeof(IDictionary<string, int>), "integer")]
    [InlineData(typeof(IReadOnlyDictionary<string, bool>), "boolean")]
    [InlineData(typeof(IDictionary), null)]
    public void GenerateSchema_GeneratesDictionarySchema_IfDictionaryType(Type type, string expectedAdditionalPropertiesType)
    {
        var schema = GetSchemaFromType(type);

        Assert.Equal("object", schema.Type);
        Assert.True(schema.AdditionalPropertiesAllowed);
        Assert.NotNull(schema.AdditionalProperties);
        Assert.Equal(expectedAdditionalPropertiesType, schema.AdditionalProperties.Type);
    }

    [Fact]
    public void GenerateSchemaForType_NullableInt()
    {
        var schema = GetSchemaFromType(typeof(int?));
        Assert.Equal("integer", schema.Type);
        Assert.True(schema.Nullable);
    }

    [Theory]
    [InlineData(typeof(int[]), "integer", "int32")]
    [InlineData(typeof(IEnumerable<string>), "string", null)]
    [InlineData(typeof(DateTime?[]), "string", "date-time")]
    [InlineData(typeof(int[][]), "array", null)]
    [InlineData(typeof(IList), null, null)]
    [InlineData(typeof(List<string>), "string", null)]
    public void GenerateSchema_GeneratesArraySchema_IfEnumerableType(
            Type type,
            string expectedItemsType,
            string expectedItemsFormat)
    {
        var schema = GetSchemaFromType(type);
        Assert.Equal("array", schema.Type);
        Assert.Equal(expectedItemsType, schema.Items.Type);
        Assert.Equal(expectedItemsFormat, schema.Items.Format);
    }

    [Theory]
    [InlineData(typeof(ISet<string>))]
    [InlineData(typeof(SortedSet<string>))]
    [InlineData(typeof(KeyedCollectionOfComplexType))]
    public void GenerateSchema_SetsUniqueItems_IfEnumerableTypeIsSetOrKeyedCollection(Type type)
    {
        var schema = GetSchemaFromType(type);

        Assert.Equal("array", schema.Type);
        Assert.True(schema.UniqueItems);
    }

    [Fact]
    public void GenerateSchemaForType_Class()
    {
        var schema = GetSchemaFromType(typeof(Person));
        Assert.Equal("object", schema.Type);
        Assert.Equal(3, schema.Properties.Count);
        Assert.Collection(schema.Properties,
            first =>
            {
                Assert.Equal("string", first.Value.Type);
                Assert.Equal("FirstName", first.Key);
            },
            second =>
            {
                Assert.Equal("string", second.Value.Type);
                Assert.Equal("lastName", second.Key);
            },
            third =>
            {
                Assert.Equal("integer", third.Value.Type);
                Assert.Equal("Age", third.Key);
            });
    }

    [Fact]
    public void GenerateSchema_IncludesInheritedProperties_IfComplexTypeIsDerived()
    {
        var schema = GetSchemaFromType(typeof(SubType1));

        Assert.Equal("object", schema.Type);
        Assert.Equal(new[] { "Property1", "BaseProperty" }, schema.Properties.Keys);
    }

    [Theory]
    [InlineData(typeof(IBaseInterface), new[] { "BaseProperty" })]
    [InlineData(typeof(ISubInterface1), new[] { "Property1", "BaseProperty" })]
    [InlineData(typeof(ISubInterface2), new[] { "Property2", "BaseProperty" })]
    [InlineData(typeof(IMultiSubInterface), new[] { "Property3", "Property2", "Property1", "BaseProperty" })]
    public void GenerateSchema_IncludesInheritedProperties_IfTypeIsAnInterfaceHierarchy(
            Type type,
            string[] expectedPropertyNames)
    {
        var schema = GetSchemaFromType(type);

        Assert.Equal("object", schema.Type);
        Assert.Equal(expectedPropertyNames.OrderBy(n => n), schema.Properties.Keys.OrderBy(k => k));
    }

    [Fact]
    public void GenerateSchema_ExcludesIndexerProperties_IfComplexTypeIsIndexed()
    {
        var schema = GetSchemaFromType(typeof(IndexedType));
        Assert.Equal("object", schema.Type);
        Assert.Equal(new[] { "Property1" }, schema.Properties.Keys);
    }

    [Theory]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.BoolWithDefault), "true")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.IntWithDefault), "2147483647")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.LongWithDefault), "9223372036854775807")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.FloatWithDefault), "3.4028235E+38")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.DoubleWithDefault), "1.7976931348623157E+308")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.StringWithDefault), "\"foobar\"")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.IntArrayWithDefault), "[\n  1,\n  2,\n  3\n]")]
    [InlineData(typeof(TypeWithDefaultAttributes), nameof(TypeWithDefaultAttributes.StringArrayWithDefault), "[\n  \"foo\",\n  \"bar\"\n]")]
    public void GenerateSchema_SetsDefault_IfPropertyHasDefaultValueAttribute(
            Type declaringType,
            string propertyName,
            string expectedDefaultAsJson)
    {
        var schema = GetSchemaFromType(declaringType);
        var propertySchema = schema.Properties[propertyName];
        Assert.NotNull(propertySchema.Default);
        Assert.Equal(expectedDefaultAsJson, propertySchema.Default.ToJson());
    }

    [Theory]
    [InlineData(typeof(TypeWithValidationAttributes))]
    // [InlineData(typeof(TypeWithValidationAttributesViaMetadataType))]

    public void GenerateSchema_SetsValidationProperties_IfComplexTypeHasValidationAttributes(Type type)
    {
        var schema = GetSchemaFromType(type);

        Assert.Equal("credit-card", schema.Properties["StringWithDataTypeCreditCard"].Format);
        Assert.Equal(1, schema.Properties["StringWithMinMaxLength"].MinLength);
        Assert.Equal(3, schema.Properties["StringWithMinMaxLength"].MaxLength);
        Assert.Equal(1, schema.Properties["ArrayWithMinMaxLength"].MinItems);
        Assert.Equal(3, schema.Properties["ArrayWithMinMaxLength"].MaxItems);
        Assert.Equal(1, schema.Properties["IntWithRange"].Minimum);
        Assert.Equal(10, schema.Properties["IntWithRange"].Maximum);
        Assert.Equal("^[3-6]?\\d{12,15}$", schema.Properties["StringWithRegularExpression"].Pattern);
        Assert.Equal(5, schema.Properties["StringWithStringLength"].MinLength);
        Assert.Equal(10, schema.Properties["StringWithStringLength"].MaxLength);
        Assert.Equal(1, schema.Properties["StringWithRequired"].MinLength);
        Assert.False(schema.Properties["StringWithRequired"].Nullable);
        Assert.False(schema.Properties["StringWithRequiredAllowEmptyTrue"].Nullable);
        Assert.Null(schema.Properties["StringWithRequiredAllowEmptyTrue"].MinLength);
        Assert.Equal(new[] { "StringWithRequired", "StringWithRequiredAllowEmptyTrue" }, schema.Required.ToArray());
    }

    [Fact]
    public void GenerateSchema_SetsReadOnlyAndWriteOnlyFlags_IfPropertyIsRestricted()
    {
        var schema = GetSchemaFromType(typeof(TypeWithRestrictedProperties));

        Assert.False(schema.Properties["ReadWriteProperty"].ReadOnly);
        Assert.False(schema.Properties["ReadWriteProperty"].WriteOnly);
        Assert.True(schema.Properties["ReadOnlyProperty"].ReadOnly);
        Assert.False(schema.Properties["ReadOnlyProperty"].WriteOnly);
        Assert.False(schema.Properties["WriteOnlyProperty"].ReadOnly);
        Assert.True(schema.Properties["WriteOnlyProperty"].WriteOnly);
    }

    [Theory]
    [InlineData(typeof(TypeWithParameterizedConstructor), nameof(TypeWithParameterizedConstructor.Id), false)]
    [InlineData(typeof(TypeWithParameterlessAndParameterizedConstructor), nameof(TypeWithParameterlessAndParameterizedConstructor.Id), true)]
    [InlineData(typeof(TypeWithParameterlessAndJsonAnnotatedConstructor), nameof(TypeWithParameterlessAndJsonAnnotatedConstructor.Id), false)]
    public void GenerateSchema_DoesNotSetReadOnlyFlag_IfPropertyIsReadOnlyButCanBeSetViaConstructor(
            Type type,
            string propertyName,
            bool expectedReadOnly
        )
    {
        var schema = GetSchemaFromType(type);
        Assert.Equal(expectedReadOnly, schema.Properties[propertyName].ReadOnly);
    }

    [Fact]
    public void GenerateSchema_HandlesTypesWithNestedTypes()
    {
        var schema = GetSchemaFromType(typeof(ContainingType));

        Assert.Equal("object", schema.Type);
        Assert.NotNull(schema.Properties["Property1"]);
        Assert.Equal("string", schema.Properties["Property1"].Properties["Property2"].Type);
    }

    private static OpenApiSchema GetSchemaFromType(Type type)
    {
        var typeInfoResolver = new DefaultJsonTypeInfoResolver();
        var jsonType = typeInfoResolver.GetTypeInfo(type, JsonSerializerOptions.Default);
        var schema = new OpenApiSchema();
        if (jsonType.Kind == JsonTypeInfoKind.None)
        {
            if (type.IsEnum)
            {
                schema = _primitiveTypeToOpenApiSchema.TryGetValue(type.GetEnumUnderlyingType(), out var enumResult)
                    ? enumResult()
                    : new OpenApiSchema { Type = "string" };
                foreach (var value in Enum.GetValues(type))
                {
                    schema.Enum.Add(new OpenApiInteger((int)value));
                }
            }
            else
            {
                schema = _primitiveTypeToOpenApiSchema.TryGetValue(type, out var result)
                    ? result()
                    : new OpenApiSchema { Type = "string" };
            }

        }
        if (jsonType.Kind == JsonTypeInfoKind.Dictionary)
        {
            schema.Type = "object";
            schema.AdditionalPropertiesAllowed = true;
            var genericTypeArgs = jsonType.Type.GetGenericArguments();
            Type? valueType = null;
            if (genericTypeArgs.Length == 2)
            {
                valueType = jsonType.Type.GetGenericArguments().Last();
            }
            schema.AdditionalProperties = _primitiveTypeToOpenApiSchema.TryGetValue(valueType, out var result)
                ? result()
                : new OpenApiSchema { };
        }
        if (jsonType.Kind == JsonTypeInfoKind.Enumerable)
        {
            schema.Type = "array";
            var elementType = jsonType.Type.GetElementType();
            schema.Items = _primitiveTypeToOpenApiSchema.TryGetValue(elementType, out var result)
                ? result()
                : new OpenApiSchema { Type = "string" };
        }
        if (jsonType.Kind == JsonTypeInfoKind.Object)
        {
            schema.Type = "object";
            foreach (var property in jsonType.Properties)
            {
                var innerSchema = GetSchemaFromType(property.PropertyType);
                var defaultValueAttribute = property.AttributeProvider.GetCustomAttributes(true).OfType<DefaultValueAttribute>().FirstOrDefault();
                if (defaultValueAttribute != null)
                {
                    innerSchema.Default = OpenApiAnyFactory.CreateFromJson(JsonSerializer.Serialize(defaultValueAttribute.Value));
                }
                innerSchema.ReadOnly = property.Set is null;
                innerSchema.WriteOnly = property.Get is null;
                schema.Properties.Add(property.Name, innerSchema);
            }
        }
        return schema;
    }
}

public class Person
{
    public string FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    public int? Age { get; set; }
}

public enum IntEnum : int
{
    Value2 = 2,
    Value4 = 4,
    Value8 = 8,
}

public enum LongEnum : long
{
    Value2 = 2,
    Value4 = 4,
    Value8 = 8,
}

public class KeyedCollectionOfComplexType : KeyedCollection<int, ComplexType>
{
    protected override int GetKeyForItem(ComplexType item)
    {
        return item.Property2;
    }
}

public class ComplexType
{
    public bool Property1 { get; set; }

    public int Property2 { get; set; }
}

public class BaseType
{
    public string BaseProperty { get; set; }
}

public class SubType1 : BaseType
{
    public int Property1 { get; set; }
}

public class SubType2 : BaseType
{
    public int Property2 { get; set; }
}

public interface IBaseInterface
{
    public string BaseProperty { get; set; }
}

public interface ISubInterface1 : IBaseInterface
{
    public int Property1 { get; set; }
}

public interface ISubInterface2 : IBaseInterface
{
    public int Property2 { get; set; }
}

public interface IMultiSubInterface : ISubInterface1, ISubInterface2
{
    public int Property3 { get; set; }
}

public class IndexedType
{
    public decimal Property1 { get; set; }

    public string this[string key1]
    {
        get { throw new NotImplementedException(); }
    }

    public string this[int key2]
    {
        get { throw new NotImplementedException(); }
    }
}

public class TypeWithDefaultAttributes
{
    [DefaultValue(true)]
    public bool BoolWithDefault { get; set; }

    [DefaultValue(int.MaxValue)]
    public int IntWithDefault { get; set; }

    [DefaultValue(long.MaxValue)]
    public long LongWithDefault { get; set; }

    [DefaultValue(float.MaxValue)]
    public float FloatWithDefault { get; set; }

    [DefaultValue(double.MaxValue)]
    public double DoubleWithDefault { get; set; }

    [DefaultValue("foobar")]
    public string StringWithDefault { get; set; }

    [DefaultValue(new[] { 1, 2, 3 })]
    public int[] IntArrayWithDefault { get; set; }

    [DefaultValue(new[] { "foo", "bar" })]
    public string[] StringArrayWithDefault { get; set; }
}

public static class OpenApiAnyFactory
{
    public static IOpenApiAny CreateFromJson(string json)
    {
        try
        {
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            return CreateFromJsonElement(jsonElement);
        }
        catch { }

        return null;
    }

    private static IOpenApiAny CreateOpenApiArray(JsonElement jsonElement)
    {
        var openApiArray = new OpenApiArray();

        foreach (var item in jsonElement.EnumerateArray())
        {
            openApiArray.Add(CreateFromJsonElement(item));
        }

        return openApiArray;
    }

    private static IOpenApiAny CreateOpenApiObject(JsonElement jsonElement)
    {
        var openApiObject = new OpenApiObject();

        foreach (var property in jsonElement.EnumerateObject())
        {
            openApiObject.Add(property.Name, CreateFromJsonElement(property.Value));
        }

        return openApiObject;
    }

    private static IOpenApiAny CreateFromJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind == JsonValueKind.Null)
        {
            return new OpenApiNull();
        }

        if (jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False)
        {
            return new OpenApiBoolean(jsonElement.GetBoolean());
        }

        if (jsonElement.ValueKind == JsonValueKind.Number)
        {
            if (jsonElement.TryGetInt32(out int intValue))
            {
                return new OpenApiInteger(intValue);
            }

            if (jsonElement.TryGetInt64(out long longValue))
            {
                return new OpenApiLong(longValue);
            }

            if (jsonElement.TryGetSingle(out float floatValue) && !float.IsInfinity(floatValue))
            {
                return new OpenApiFloat(floatValue);
            }

            if (jsonElement.TryGetDouble(out double doubleValue))
            {
                return new OpenApiDouble(doubleValue);
            }
        }

        if (jsonElement.ValueKind == JsonValueKind.String)
        {
            return new OpenApiString(jsonElement.ToString());
        }

        if (jsonElement.ValueKind == JsonValueKind.Array)
        {
            return CreateOpenApiArray(jsonElement);
        }

        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            return CreateOpenApiObject(jsonElement);
        }

        throw new System.ArgumentException($"Unsupported value kind {jsonElement.ValueKind}");
    }
}

public static class IOpenApiAnyExtensions
{
    public static string ToJson(this IOpenApiAny openApiAny)
    {
        var stringWriter = new StringWriter();
        var jsonWriter = new OpenApiJsonWriter(stringWriter);

        openApiAny.Write(jsonWriter, OpenApiSpecVersion.OpenApi3_0);

        return stringWriter.ToString();
    }
}

public class TypeWithValidationAttributes
{
    [DataType(DataType.CreditCard)]
    public string StringWithDataTypeCreditCard { get; set; }

    [MinLength(1), MaxLength(3)]
    public string StringWithMinMaxLength { get; set; }

    [MinLength(1), MaxLength(3)]
    public string[] ArrayWithMinMaxLength { get; set; }

    [Range(1, 10)]
    public int IntWithRange { get; set; }

    [RegularExpression("^[3-6]?\\d{12,15}$")]
    public string StringWithRegularExpression { get; set; }

    [StringLength(10, MinimumLength = 5)]
    public string StringWithStringLength { get; set; }

    [Required]
    public string StringWithRequired { get; set; }

    [Required(AllowEmptyStrings = true)]
    public string StringWithRequiredAllowEmptyTrue { get; set; }
}

public class TypeWithRestrictedProperties
{
    public int ReadWriteProperty { get; set; }
    public int ReadOnlyProperty { get; }
    public int WriteOnlyProperty { set { } }
}

public class TypeWithParameterizedConstructor
{
    public TypeWithParameterizedConstructor(int id, string desc)
    {
        Id = id;
        Description = desc;
    }

    public int Id { get; }
    public string Description { get; }
}

public class TypeWithParameterlessAndParameterizedConstructor
{
    public TypeWithParameterlessAndParameterizedConstructor()
    { }

    public TypeWithParameterlessAndParameterizedConstructor(int id, string desc)
    {
        Id = id;
        Description = desc;
    }

    public int Id { get; }
    public string Description { get; }
}

public class TypeWithParameterlessAndJsonAnnotatedConstructor
{
    public TypeWithParameterlessAndJsonAnnotatedConstructor()
    { }

    [JsonConstructor]
    public TypeWithParameterlessAndJsonAnnotatedConstructor(int id, string desc)
    {
        Id = id;
        Description = desc;
    }

    public int Id { get; }
    public string Description { get; }
}

public class ContainingType
{
    public NestedType Property1 { get; set; }

    public class NestedType
    {
        public string Property2 { get; set; }
    }
}
