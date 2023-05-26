// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Components.Forms;

public class CascadingFormParameterValue : BaseCascadingFormParameterValue
{
    [Inject] private IFormValueSupplier FormValueSupplier { get; set; } = null!;

    protected override bool TryBindValue(string formName, Type valueType, out object? value)
    {
        // Can't supply the value if this context is for a form with a different name.
        if (FormValueSupplier.CanBind(formName!, valueType))
        {
            return FormValueSupplier.TryBind(formName!, valueType, out value);
        }

        value = null;
        return false;
    }
}
