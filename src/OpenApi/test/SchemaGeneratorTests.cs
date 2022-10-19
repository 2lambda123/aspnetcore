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

namespace Microsoft.AspNetCore.OpenApi.Tests;

public class SchemaGeneratorTests
{
    private static readonly Dictionary<Type, Func<OpenApiSchema>> _primitiveTypeToOpenApiSchema =
        new Dictionary<Type, Func<OpenApiSchema>>
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

    [Fact]
    public void GenerateSchemaForType_DedupsEnumValues_IfEnumTypeHasDuplicateValues()
    {
        var enumType = typeof(HttpStatusCode);
        var schema = GetSchemaFromType(enumType);
        Assert.Equal(enumType.GetEnumValues().Cast<HttpStatusCode>().Count(), schema.Enum.Count);
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
    }

    [Fact]
    public void GenerateSchemaForType_ArrayOfInt()
    {
        var schema = GetSchemaFromType(typeof(int[]));
        Assert.Equal("array", schema.Type);
        Assert.Equal("integer", schema.Items.Type);
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
            var elementType = jsonType.Type.GetElementType();
            schema.AdditionalProperties.Type = _primitiveTypeToOpenApiSchema.TryGetValue(elementType, out var result)
                ? result().Type
                : "string";
        }
        if (jsonType.Kind == JsonTypeInfoKind.Enumerable)
        {
            schema.Type = "array";
            var elementType = jsonType.Type.GetElementType();
            schema.Items.Type = _primitiveTypeToOpenApiSchema.TryGetValue(elementType, out var result)
                ? result().Type
                : "string";
        }
        if (jsonType.Kind == JsonTypeInfoKind.Object)
        {
            schema.Type = "object";
            foreach (var property in jsonType.Properties)
            {
                var innerSchema = GetSchemaFromType(property.PropertyType);
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
