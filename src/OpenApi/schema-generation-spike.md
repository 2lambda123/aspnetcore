# JSON Schema Generation in ASP.NET

### Problem Statement

The generation of schemas from .NET types is a core component of the OpenAPI experience in ASP.NET.

### Proposed Solution

System.Text.Json shipped with support for a type resolver API that provides primitives to aid in the construction of JSON schemas. The purpose of this spike is to explore whether this API would provide a viable abstraction for integrating JSON schema generation in `Microsoft.OpenApi`, `Microsoft.AspNetCore.OpenApi`, or as a replacement to the existing schema generation in `Swashbuckle.AspNetCore`.

The goal of the spike was to evaluate the implementation complexity and completeness of this API when generating schemas for types that are currently supported by `Swashbuckle.AspNetCore`. `SchemaGeneratorTests` contains tests for some of the scenarios that we want to validate. So far, here are some of the challenges that have been identified with leveraging this implementation:

- Getting the key types of dictionaries can be cumbersome. It would be great if the `JsonTypeInfo` class provided a `KeyType` property that could provide information about the type of `K` in `Dictionary<K, V>`
