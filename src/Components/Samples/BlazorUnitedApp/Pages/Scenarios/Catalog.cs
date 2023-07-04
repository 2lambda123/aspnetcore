// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace BlazorUnitedApp.Pages.Scenarios;

public class Catalog
{
    public static readonly Catalog Instance = new Catalog()
    {
        Products = Enumerable.Range(1, 10).Select(i => new Product
        {
            Id = i,
            Name = $"Product {i}",
            Price = 10 * i,
            RemainingUnits = 2 * i
        }).ToList()
    };

    public List<Product> Products { get; set; } = new();
}
