// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.Server.Circuits;

namespace Microsoft.AspNetCore.Components.Server;

public class CircuitLocal<T> where T : class
{

    public T? Value
    {
        get { return CircuitLocal._hostContext.Value.CircuitContext.Get<T>(); }
        set { CircuitLocal._hostContext.Value.CircuitContext.Set<T>(value); }
    }

}

internal static class CircuitLocal
{
    internal static AsyncLocal<CircuitHost> _hostContext = new AsyncLocal<CircuitHost>();
}

internal class CircuitContext
{
    public Dictionary<Type, object> Data { get; set; } = new();

    public T? Get<T>() where T : class => (T?)Data?.GetValueOrDefault(typeof(T));

    internal void Set<T>(T? value)
    {
        Data[typeof(T)] = value;
    }
}
