// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseOpenApi(); // Can we auto-add the options here
builder.Services.Configure<OpenApiDocument>(document =>
{
    document.Tags = new List<OpenApiTag>() { new OpenApiTag { Name = "test-tag" } };
});

var app = builder.Build();

string Plaintext() => "Hello, World!";
app.MapGet("/plaintext", Plaintext);

object Json() => new { message = "Hello, World!" };
app.MapGet("/json", Json).WithTags("json");

string SayHello(string name) => $"Hello, {name}!";
app.MapGet("/hello/{name}", SayHello);

app.MapGet("/null-result", IResult () => null!);

app.MapGet("/todo/{id}", Results<Ok<Todo>, NotFound, BadRequest> (int id) => id switch
    {
        <= 0 => TypedResults.BadRequest(),
        >= 1 and <= 10 => TypedResults.Ok(new Todo(id, "Walk the dog")),
        _ => TypedResults.NotFound()
    });

app.MapGet("/swagger", ([FromServices] IOptions<OpenApiDocument> openApiDocument) =>
{
    return openApiDocument.Value.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
});

app.Run();

internal record Todo(int Id, string Title);
public class TodoBindable : IBindableFromHttpContext<TodoBindable>
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }

    public static ValueTask<TodoBindable?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        return ValueTask.FromResult<TodoBindable?>(new TodoBindable { Id = 1, Title = "I was bound from IBindableFromHttpContext<TodoBindable>.BindAsync!" });
    }
}
