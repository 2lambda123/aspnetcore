// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Microsoft.AspNetCore;

internal class ConfigurationUnwrapper : IConfigurationRoot, IDisposable
{
    private readonly IConfigurationRoot _configurationRoot;

    public ConfigurationUnwrapper(IConfigurationRoot configurationRoot)
    {
        _configurationRoot = configurationRoot;
    }

    public string this[string key]
    {
        get => _configurationRoot[key];
        set => _configurationRoot[key] = value;
    }

    public IEnumerable<IConfigurationProvider> Providers =>
        _configurationRoot.Providers.Select(p => (p as IgnoreFirstLoadConfigurationProvider)?.OriginalConfigurationProvider ?? p);

    public IEnumerable<IConfigurationSection> GetChildren() => _configurationRoot.GetChildren();

    public IChangeToken GetReloadToken() => _configurationRoot.GetReloadToken();

    public IConfigurationSection GetSection(string key) => _configurationRoot.GetSection(key);

    public void Reload() => _configurationRoot.Reload();

    public void Dispose() => (_configurationRoot as IDisposable)?.Dispose();
}
