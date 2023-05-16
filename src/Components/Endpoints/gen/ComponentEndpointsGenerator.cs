// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.Components.Endpoints.Generator;

[Generator]
public sealed class ComponentEndpointsGenerator : IIncrementalGenerator
{
    public ComponentEndpointsGenerator()
    {
    }
    // We are going to generate a file like this one (with some simplifications in the sample),
    // assuming an app Blazor.United.Assembly and a library Razor.Class.Library:
    //[assembly: AppRazorComponentApplication]
    //namespace Microsoft.AspNetCore.Components.Infrastructure;

    //file class AppRazorComponentApplicationAttribute : RazorComponentApplicationAttribute
    //{
    //    public override ComponentApplicationBuilder GetBuilder()
    //    {
    //        var builder = new ComponentApplicationBuilder();
    //        builder.AddLibrary(GetBlazorUnitedAssemblyBuilder());
    //        builder.AddLibrary(GetRazorClassLibraryBuilder());
    //        return builder;
    //    }
    //
    //    private ComponentLibraryBuilder GetBlazorUnitedAssemblyBuilder()
    //    {
    //        var source = "Blazor.United.Assembly";
    //        return new ComponentLibraryBuilder(
    //              source,
    //              GetBlazorUnitedAssemblyPages(source),
    //              GetBlazorUnitedAssemblyComponents(source));
    //    }
    //
    //    private IEnumerable<PageComponentBuilder> GetBlazorUnitedAssemblyPages()
    //    {
    //        yield return new PageComponentBuilder()
    //        {
    //            Source = "Blazor.United.Assembly",
    //            PageType = typeof(Counter),
    //            RouteTemplates = new List<string> { "/counter" }
    //        };
    //        ...
    //    }
    //    ...
    //}
    // This approach has been chosen for a couple of reasons:
    // 1) We want to avoid creating very big methods at compile time (We might even need to split Get...(Pages|Components) into chunks
    //    to limit the method size.
    // 2) We want the source generator to be as incremental as possible, so we are going to compute the thunk bodies individually and reuse
    //    them when possible. The structure above, mostly relates to the following thunks:
    // pagesTunk:
    //     yield return new PageComponentBuilder(string source)
    //     {
    //         Source = source,
    //         PageType = typeof(Counter),
    //         RouteTemplates = new List<string> { "/counter" }
    //     };
    //     ...
    // libraryThunk:
    //    private ComponentLibraryBuilder GetBlazorUnitedAssemblyBuilder()
    //    {
    //        var source = "Blazor.United.Assembly";
    //        return new ComponentLibraryBuilder(
    //              source,
    //              GetBlazorUnitedAssemblyPages(source),
    //              GetBlazorUnitedAssemblyComponents(source));
    //    }
    //
    //    private IEnumerable<PageComponentBuilder> GetBlazorUnitedAssemblyPages()
    //    {
    //        <<pagesThunk>>
    //    }
    // appThunk:
    //    var builder = new ComponentApplicationBuilder();
    //    builder.AddLibrary(GetBlazorUnitedAssemblyBuilder());
    //    builder.AddLibrary(GetRazorClassLibraryInfo());
    //    return builder;
    //
    //
    // appThunk only changes with renames.
    // libraryThunk only changes when the project is renamed
    // pagesThunk changes when a page is added, removed or renamed, and so on.
    // This drives the way we approach writing the code for the source generator, since we favor lots of small methods and
    // combinations over big and coarse methods that compute the entire contents.
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var componentsAssemblyIdentity = context.CompilationProvider
            .Select((c, t) => c.ReferencedAssemblyNames.SingleOrDefault(ai => ai.Name == "Microsoft.AspNetCore.Components"));

        var componentsAssemblySymbol = context.CompilationProvider.Combine(componentsAssemblyIdentity)
            .Select(FindComponentsAssemblySymbol);

        var componentInterface = componentsAssemblySymbol
            .Select((assembly, t) => ResolveComponentsCompilationContext(assembly));

        var componentsFromProject = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: (sn, ct) => sn is ClassDeclarationSyntax cls &&
                sn.IsKind(SyntaxKind.ClassDeclaration) &&
                cls.BaseList != null && cls.BaseList.Types.Count > 0,
                transform: (ctx, ct) => ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, cancellationToken: ct))
            .Combine(componentInterface)
            .Where(IsComponent)
            .Select(CreateComponentModel)
            .Combine(context.CompilationProvider.Select((c, ct) => c.Assembly))
            .Select((pair, ct) => (pair.Right, pair.Left));

        var compilationReferences = context.CompilationProvider
            .SelectMany((c, t) => c.References.Select(r => (IAssemblySymbol)c.GetAssemblyOrModuleSymbol(r)!));

        var assembliesReferencingComponents = compilationReferences.Combine(componentsAssemblyIdentity)
            .Where(FilterAssemblies)
            .WithTrackingName("AssembliesWithComponentsAssemblyReference");

        var getLibraryComponentMethodThunks =
            assembliesReferencingComponents.Select((arc, ct) => CreateGetLibraryMethodThunk(arc.Left));

        var appGetLibraryComponentMethodThunk = context.CompilationProvider
            .Select((c, ct) => CreateGetLibraryMethodThunk(c.Assembly));

        var referencesGetLibraryThunk =
            assembliesReferencingComponents
            .Select((arc, ct) => CreateLibraryThunk(arc.Left!));

        var appGetLibraryThunk = context.CompilationProvider
            .Select((c, ct) => CreateLibraryThunk(c.Assembly));

        var getBuilderThunk = referencesGetLibraryThunk.Collect().Combine(appGetLibraryThunk).Select((t, ct) => t.Left.Add(t.Right))
            .Select((getLibraryThunks, ct) => CreateGetBuilderThunk(getLibraryThunks));

        var allComponentDefinitions = assembliesReferencingComponents
            .Select(((IAssemblySymbol candidate, AssemblyIdentity identity) context, CancellationToken cancellation) => context.candidate)
            .Combine(componentInterface)
            .SelectMany(ComponentWithAssembly);

        var getPagesSignature = assembliesReferencingComponents.Select(
            (t, ct) => (assembly: t.Left!, signature: CreateGetPagesMethodSignature(t.Left!)));

        var projectGetPagesSignature = context.CompilationProvider.Select((c, ct) => c.Assembly)
            .Select((assembly, ct) => (assembly, signature: CreateGetPagesMethodSignature(assembly)));

        var projectGetComponentPagesBodyThunk = componentsFromProject
            .Where(cfp => cfp.Left.IsPage)
            .Select((cm, ct) => GetPagesBody(cm.Left))
            .Collect()
            .Combine(projectGetPagesSignature)
            .Select((ctx, ct) => CreateGetMethod(ctx.Right.signature, ctx.Left));

        var getComponentPagesBodyThunk = allComponentDefinitions
            .Where(c => c.component.IsPage)
            .Select((cm, ct) => (cm.assembly, body: GetPagesBody(cm.component)));

        var groupedComponentPageStatements = getComponentPagesBodyThunk
            .Collect()
            .Select((gpbt, ct) => gpbt.GroupBy(kvp => kvp.assembly, SymbolEqualityComparer.Default)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Select(t => t.body).ToImmutableArray(), SymbolEqualityComparer.Default));

        var empty = ImmutableArray.Create("yield break;");
        var getPagesMethodThunks = getPagesSignature
            .Combine(groupedComponentPageStatements)
            .Select((ctx, ct) =>
            {
                var (assembly, signature) = ctx.Left;
                var bodyStatements = ctx.Right;
                var body = bodyStatements.TryGetValue(assembly, out var found) ? found : empty;
                return CreateGetMethod(signature, body);
            });

        var projectGetComponentsSignature = context.CompilationProvider.Select((c, ct) => c.Assembly)
            .Select((assembly, ct) => (assembly, signature: CreateGetComponentsMethodSignature(assembly)));

        var projectGetComponentComponentsBodyThunk = componentsFromProject
            .Select((cm, ct) => GetComponentsBody(cm.Left))
            .Collect()
            .Combine(projectGetComponentsSignature)
            .Select((ctx, ct) => CreateGetMethod(ctx.Right.signature, ctx.Left));

        var getComponentsSignature = assembliesReferencingComponents.Select(
            (t, ct) => (assembly: t.Left!, signature: CreateGetComponentsMethodSignature(t.Left!)));

        var getComponentsBodyThunk = allComponentDefinitions
            .Select((cm, ct) => (cm.assembly, body: GetComponentsBody(cm.component)));

        var groupedComponentStatements = getComponentsBodyThunk
            .Collect()
            .Select((gpbt, ct) => gpbt.GroupBy(kvp => kvp.assembly, SymbolEqualityComparer.Default)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Select(t => t.body).ToImmutableArray(), SymbolEqualityComparer.Default));

        var getComponentMethodThunks = getComponentsSignature
            .Combine(groupedComponentStatements)
            .Select((ctx, ct) =>
            {
                var (assembly, signature) = ctx.Left;
                var bodyStatements = ctx.Right;
                var body = bodyStatements[assembly];
                return CreateGetMethod(signature, body);
            });

        var allThunks = getLibraryComponentMethodThunks.Collect().Combine(appGetLibraryComponentMethodThunk)
            .Select((pair, ct) => pair.Left.Add(pair.Right))
            .Combine(getBuilderThunk)
            .Select((pair, ct) => pair.Left.Add(pair.Right))
            .Combine(getPagesMethodThunks.Collect())
            .Select((pair, ct) => pair.Left.AddRange(pair.Right))
            .Combine(getComponentMethodThunks.Collect())
            .Select((pair, ct) => pair.Left.AddRange(pair.Right))
            .Combine(projectGetComponentPagesBodyThunk)
            .Select((pair, ct) => pair.Left.Add(pair.Right))
            .Combine(projectGetComponentComponentsBodyThunk)
            .Select((pair, ct) => pair.Left.Add(pair.Right));

        context.RegisterImplementationSourceOutput(allThunks, (spc, thunks) =>
        {
            var stringBuilder = new StringBuilder();
            using var stringWriter = new StringWriter(stringBuilder);
            var codeWriter = new CodeWriter(stringWriter, 0);
            codeWriter.WriteLine(ComponentEndpointsGeneratorSources.SourceHeader);
            codeWriter.WriteLine();
            codeWriter.WriteLine(ComponentEndpointsGeneratorSources.RazorComponentApplicationAssemblyAndNamespaceDeclaration);
            codeWriter.WriteLine();
            codeWriter.WriteLine(ComponentEndpointsGeneratorSources.GeneratedCodeAttribute);
            codeWriter.WriteLine(ComponentEndpointsGeneratorSources.RazorComponentApplicationAttributeFileHeader);
            codeWriter.StartBlock();
            for (var i = 0; i < thunks.Length - 1; i++)
            {
                var thunk = thunks[i];
                codeWriter.WriteLine(thunk);
            }
            codeWriter.Write(thunks[thunks.Length - 1]);
            codeWriter.EndBlock(newLine: false);

            codeWriter.Flush();
            stringWriter.Flush();

            var fileText = stringBuilder.ToString();

            spc.AddSource("Components.Discovery.cs", fileText);
        });
    }

    private string GetComponentsBody(ComponentModel cm)
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

    private string CreateGetComponentsMethodSignature(IAssemblySymbol assembly)
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

    private string CreateGetMethod(string signature, ImmutableArray<string> body)
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

    private IEnumerable<(IAssemblySymbol assembly, ComponentModel component)> ComponentWithAssembly(
        (IAssemblySymbol? assembly, ComponentsCompilationContext componentContext) context, CancellationToken cancellation)
    {
        var (assembly, componentContext) = context;
        if (assembly == null || componentContext.ComponentInterface == null)
        {
            yield break;
        }

        var module = assembly.Modules.Single();

        var componentCollector = new ComponentCollector
        {
            Context = componentContext
        };

        componentCollector.Visit(module.GlobalNamespace);

        foreach (var item in componentCollector.Components!)
        {
            yield return (assembly, item);
        }
    }

    private string GetPagesBody(ComponentModel cm)
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

    private string CreateGetPagesMethodSignature(IAssemblySymbol assembly)
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

    private string CreateGetLibraryMethodThunk(IAssemblySymbol assembly)
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

    private string CreateGetBuilderThunk(ImmutableArray<string> getLibraryThunks)
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

    private string CreateLibraryThunk(IAssemblySymbol assembly)
    {
        var name = assembly.Name.Replace(".", "_");
        return $"builder.AddLibrary(Get{name}Builder());";
    }

    private ComponentModel CreateComponentModel((ISymbol? Left, ComponentsCompilationContext Right) tuple, CancellationToken token)
    {
        var (component, componentContext) = tuple;

        return new ComponentModel(
            (INamedTypeSymbol)component!,
            component!
                .GetAttributes()
                .FirstOrDefault(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, componentContext.RouteAttribute)));
    }

    private bool IsComponent((ISymbol? Left, ComponentsCompilationContext Right) tuple)
    {
        return tuple.Left is INamedTypeSymbol componentType &&
            ComponentCollector.IsComponent(componentType, tuple.Right.ComponentInterface);
    }

    private static ComponentsCompilationContext ResolveComponentsCompilationContext(IAssemblySymbol? assembly)
    {
        var componentInterface = assembly?.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent");
        var routeAttribute = assembly?.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RouteAttribute");

        return new ComponentsCompilationContext(assembly!, componentInterface!, routeAttribute!);
    }

    private IAssemblySymbol? FindComponentsAssemblySymbol((Compilation, AssemblyIdentity) tuple, CancellationToken token)
    {
        var (compilation, identity) = tuple;
        // This assumes a C# compilation
        var module = compilation.Assembly.Modules.Single();

        foreach (var assembly in module.ReferencedAssemblySymbols)
        {
            if (assembly.Identity == identity)
            {
                return assembly;
            }
        }

        return null;
    }

    private bool FilterAssemblies((IAssemblySymbol assembly, AssemblyIdentity componentsAssembly) context)
    {
        var (assembly, componentsAssembly) = context;
        if (assembly.Name.StartsWith("System.", StringComparison.Ordinal) ||
            assembly.Name.StartsWith("Microsoft.", StringComparison.Ordinal))
        {
            // Filter out system assemblies as well as our components assemblies.
            return false;
        }

        if (assembly.Modules.Skip(1).Any())
        {
            return false;
        }
        var module = assembly.Modules.SingleOrDefault();
        if (module == null)
        {
            return false;
        }

        foreach (var refIdentity in module.ReferencedAssemblies)
        {
            if (refIdentity == componentsAssembly)
            {
                return true;
            }
        }

        return false;
    }
}

internal class AssemblyModel
{
    private IAssemblySymbol _assembly;
    private ImmutableArray<ComponentModel>? _components;

    public AssemblyModel(IAssemblySymbol assembly, ImmutableArray<ComponentModel> components)
    {
        Assembly = assembly;
        Components = components;
    }

    public IAssemblySymbol Assembly { get => _assembly; set => _assembly = value; }
    public ImmutableArray<ComponentModel>? Components { get => _components; set => _components = value; }
}

public class ComponentCollector : SymbolVisitor
{
    public ComponentCollector()
    {
    }

    public System.Collections.Generic.List<ComponentModel>? Components { get; set; }

    public ComponentsCompilationContext? Context { get; set; }

    public override void VisitNamespace(INamespaceSymbol symbol)
    {
        foreach (var member in symbol.GetMembers())
        {
            member.Accept(this);
        }
    }

    public override void VisitNamedType(INamedTypeSymbol symbol)
    {
        if (Context == null)
        {
            throw new InvalidOperationException("Missing context");
        }

        if (IsComponent(symbol, Context.ComponentInterface))
        {
            Components ??= new();
            Components.Add(
                new ComponentModel(
                    symbol,
                    symbol.GetAttributes().FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, Context.RouteAttribute))));
        }
    }

    internal static bool IsComponent(INamedTypeSymbol candidate, INamedTypeSymbol componentInterface)
    {
        if (candidate.TypeKind != TypeKind.Class ||
            candidate.IsAbstract ||
            candidate.IsAnonymousType ||
            candidate.DeclaredAccessibility != Accessibility.Public ||
            string.Equals(candidate.Name, "_Imports", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var t in candidate.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(t, componentInterface))
            {
                return true;
            }
        }

        return false;
    }
}

public record ComponentsCompilationContext(
    IAssemblySymbol? Assembly,
    INamedTypeSymbol ComponentInterface,
    INamedTypeSymbol RouteAttribute);

public record ComponentModel(INamedTypeSymbol Component, AttributeData? RouteAttribute)
{
    public bool IsPage => RouteAttribute != null;
}
