// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BlazorUnitedApp;
using BlazorUnitedApp.Data;
using BlazorUnitedApp.Pages;
using Microsoft.AspNetCore.Components.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<DynamicComponentResolver<object>, CustomResolver>();
builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapDynamicRazorComponentEndpoints<CustomResolver, object>("{**path:nonfile}", null);
//app.MapRazorComponents<App>();

app.Run();

public class CustomResolver : DynamicComponentResolver<object>
{
    public override ValueTask<ResolverResult> ResolveComponentAsync(
        HttpContext httpContext,
        RouteValueDictionary values,
        object? state)
    {
        return new ValueTask<ResolverResult>(new ResolverResult(typeof(App), typeof(Counter), new RouteValueDictionary()));
    }
}
