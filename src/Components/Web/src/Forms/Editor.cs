// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Rendering;

namespace Microsoft.AspNetCore.Components.Forms;
/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class Editor<T> : ComponentBase
{
    private HtmlFieldPrefix? _value;

    /// <summary>
    /// 
    /// </summary>
    [Parameter] public T Value { get; set; } = default!;

    /// <summary>
    /// 
    /// </summary>
    [Parameter] public Expression<Func<T>> ValueExpression { get; set; } = default!;

    /// <summary>
    /// 
    /// </summary>
    [Parameter] public EventCallback<T> ValueChanged { get; set; } = default!;

    [CascadingParameter] private HtmlFieldPrefix FieldPrefix { get; set; } = default!;

    /// <summary>
    /// 
    /// </summary>
    protected override void OnParametersSet()
    {
        _value = FieldPrefix != null ? FieldPrefix.Combine(ValueExpression) : new HtmlFieldPrefix(ValueExpression);
    }

    private protected override void RenderCore(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<HtmlFieldPrefix>>(0);
        builder.AddAttribute(1, "Value", _value);
        builder.AddAttribute(2, "IsFixed", true);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)BuildRenderTree);
        builder.CloseComponent();
    }
}
