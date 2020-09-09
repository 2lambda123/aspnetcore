// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    public class Drag<TItem> : ComponentBase, IAsyncDisposable
    {
        // TODO: Could pass the item around via DotNetObjectReference, but that requires it to be a reference
        // type, which is a rather limiting constraint.

        private long _id;

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public TItem Item { get; set; } = default!;

        [Parameter]
        public Action<TItem, DragEventArgs>? OnDragStart { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var interopRelayReference = DotNetObjectReference.Create(new DragInteropRelay<TItem>(this));
            _id = await JSRuntime.InvokeAsync<long>("_blazorDragAndDrop.registerDrag", interopRelayReference);
        }

        protected override void OnParametersSet()
        {
            if (Item is null)
            {
                throw new InvalidOperationException($"{GetType()} requires that parameter '{nameof(Item)}' be non-null.");
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "draggable", "true");
            builder.AddContent(2, ChildContent);
            builder.CloseElement();
        }

        internal (DragEventArgs, TItem) OnDragStartCore(DragEventArgs e)
        {
            OnDragStart?.Invoke(Item, e);

            return (e, Item);
        }

        public ValueTask DisposeAsync()
            => JSRuntime.InvokeVoidAsync("_blazorDragAndDrop.unregisterDrag", _id);
    }
}
