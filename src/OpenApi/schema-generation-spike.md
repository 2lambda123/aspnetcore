# JSON Schema Generation in ASP.NET

### Problem Statement

The generation of schemas from .NET types is a core component of the OpenAPI experience in ASP.NET.

### Explored Soultion

System.Text.Json shipped with support for a type resolver API that provides primitives to aid in the construction of JSON schemas. The purpose of this spike is to explore whether this API would provide a viable abstraction for integrating JSON schema generation in `Microsoft.OpenApi`, `Microsoft.AspNetCore.OpenApi`, or as a replacement to the existing schema generation in `Swashbuckle.AspNetCore`.

The goal of the spike was to evaluate the implementation complexity and completeness of this API when generating schemas for types that are currently supported by `Swashbuckle.AspNetCore`. `SchemaGeneratorTests` contains tests for some of the scenarios that we want to validate. So far, here are some of the challenges that have been identified with leveraging this implementation.

**Constructing schemas for dictionary types**

`JsonTypeInfo` does not provide much helpful information when it comes to resolving the types of dictionary keys and values. Instead, the API user must resolve to processing the generic type arguments of the `Dictionary`-based types themselves in order to extract this info. Similar to what is done for `JsonTypeInfoKind.Object`, it would be helpful if the resolver provided an API for inspecting the key and value types of dictionaries.

**Constructing schemas for sequence-based types**

There's no consistent mechanism for extracting the item-type for enumerables. Primitive, like array, allow extracting the type of elements via `Type.GetElementType` but reference types like `IEnumerable` and `List` require inspecting the generic arguments of the types to resolve element types. A consistent strategy for getting the type within a list would make API implementation much easier.

**Determining whether `UniqueItems` should be set to true**

Determining whether or not `UniqueItems` should be set to true requires knowing if the collection type is a descandant of `ISet` or if it is a keyed collection.

**Does not capture inherited properties in interface hierarchy**

https://github.com/dotnet/runtime/issues/77276 and https://github.com/dotnet/runtime/issues/41749.

**Processing `ValidationAttributes` into schema**

Data annotations aren't captured in any meaningful way (outside of the attributes) in the `JsonTypeInfo`. This information can be resolved manually by inspecting the validation attributes and generating the appropriate schema attributes.

