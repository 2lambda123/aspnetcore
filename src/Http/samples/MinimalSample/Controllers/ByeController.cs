// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Mvc;

namespace MinimalSample.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ByeController : ControllerBase
{
    [HttpGet("{name}")]
    public string Get(string name)
    {
        return $"Bye {name}!";
    }
}
