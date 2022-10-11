// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapGet("/hello/{name}", (string name) => $"Hello {name}!")
    .AddEndpointFilterFactory((context, next) =>
    {
        var parameters = context.MethodInfo.GetParameters();
        // Only operate handlers with a single argument
        if (parameters.Length == 1 &&
            parameters[0] is ParameterInfo parameter &&
            parameter.ParameterType == typeof(string))
        {
            return invocationContext =>
            {
                var modifiedArgument = invocationContext
                    .GetArgument<string>(0)
                    .ToUpperInvariant();
                invocationContext.Arguments[0] = modifiedArgument;
                return next(invocationContext);
            };
        }

        return invocationContext => next(invocationContext);
    });

app.MapControllers()
    .AddEndpointFilter((invocationContext, next) =>
    {
        var argument = invocationContext.GetArgument<string>(0);
        if (argument != null)
        {
            invocationContext.Arguments[0] = Convert.ToBase64String(Encoding.UTF8.GetBytes(argument));
        }
        return next(invocationContext);
    });

app.Run();
