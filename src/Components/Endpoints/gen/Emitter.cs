// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.AspNetCore.Components.Endpoints.Generator;
internal class Emitter
{
    internal static string GetComponentsBody(ComponentModel cm)
    {
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 2);
        codeWriter.WriteLine("yield return new global::Microsoft.AspNetCore.Components.ComponentBuilder");
        codeWriter.StartBlock();
        codeWriter.WriteLine($"Source = source,");
        codeWriter.WriteLine($"ComponentType = typeof({cm.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}),");
        codeWriter.EndBlockWithSemiColon(newLine: false);
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateGetComponentsMethodSignature(IAssemblySymbol assembly)
    {
        var name = assembly.Name.Replace(".", "_");
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 1);
        var returnType = "global::System.Collections.Generic.IEnumerable<global::Microsoft.AspNetCore.Components.ComponentBuilder>";
        codeWriter.Write($"private {returnType} Get{name}Components(string source)");
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateGetMethod(string signature, ImmutableArray<string> body)
    {
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 1);
        codeWriter.WriteLine(signature);
        codeWriter.StartBlock();
        for (var i = 0; i < body.Length; i++)
        {
            var definition = body[i];
            codeWriter.WriteLine(definition);
        }
        codeWriter.EndBlock();
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string GetPagesBody(ComponentModel cm)
    {
        //        yield return new PageComponentBuilder()
        //        {
        //            Source = "Blazor.United.Assembly",
        //            PageType = typeof(Counter),
        //            RouteTemplates = new List<string> { "/counter" }
        //        };
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 2);
        codeWriter.WriteLine("yield return new global::Microsoft.AspNetCore.Components.PageComponentBuilder");
        codeWriter.StartBlock();
        codeWriter.WriteLine($"Source = source,");
        codeWriter.WriteLine($"PageType = typeof({cm.Component.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}),");
        codeWriter.WriteLine($$"""RouteTemplates = new global::System.Collections.Generic.List<string>{ {{cm.RouteAttribute!.ConstructorArguments[0].ToCSharpString()}} }""");
        codeWriter.EndBlockWithSemiColon(newLine: false);
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateGetPagesMethodSignature(IAssemblySymbol assembly)
    {
        var name = assembly.Name.Replace(".", "_");
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 1);
        var returnType = "global::System.Collections.Generic.IEnumerable<global::Microsoft.AspNetCore.Components.PageComponentBuilder>";
        codeWriter.Write($"private {returnType} Get{name}Pages(string source)");
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateGetLibraryMethodThunk(IAssemblySymbol assembly)
    {
        var name = assembly.Name.Replace(".", "_");
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 1);
        var returnType = "global::Microsoft.AspNetCore.Components.ComponentLibraryBuilder";
        codeWriter.WriteLine($"private {returnType} Get{name}Builder()");
        codeWriter.StartBlock();
        codeWriter.WriteLine($"var source = \"{assembly.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}\";");
        codeWriter.Write("return new global::Microsoft.AspNetCore.Components.ComponentLibraryBuilder");
        codeWriter.StartParameterListBlock();
        codeWriter.WriteLine("source,");
        codeWriter.WriteLine($"Get{name}Pages(source),");
        codeWriter.Write($"Get{name}Components(source)");
        codeWriter.EndParameterListBlock();
        codeWriter.WriteLine(";");
        codeWriter.EndBlock();
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateGetBuilderThunk(ImmutableArray<string> getLibraryThunks)
    {
        var builder = new StringBuilder();
        var writer = new StringWriter(builder);
        var codeWriter = new CodeWriter(writer, 1);
        codeWriter.WriteLine("public override global::Microsoft.AspNetCore.Components.ComponentApplicationBuilder GetBuilder()");
        codeWriter.StartBlock();
        codeWriter.WriteLine("var builder = new global::Microsoft.AspNetCore.Components.ComponentApplicationBuilder();");
        for (var i = 0; i < getLibraryThunks.Length; i++)
        {
            codeWriter.WriteLine(getLibraryThunks[i]);
        }
        codeWriter.WriteLine("return builder;");
        codeWriter.EndBlock();
        codeWriter.Flush();
        writer.Flush();
        return builder.ToString();
    }

    internal static string CreateLibraryThunk(IAssemblySymbol assembly)
    {
        var name = assembly.Name.Replace(".", "_");
        return $"builder.AddLibrary(Get{name}Builder());";
    }
}
