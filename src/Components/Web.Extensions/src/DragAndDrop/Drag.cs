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
        public Action<TItem, MutableDragEventArgs>? OnDragStart { get; set; }

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
            builder.AddAttribute(2, "ondragstart", $"window._blazorDragAndDrop.onDragStart(event, {_id})");
            builder.AddContent(3, ChildContent);
            builder.CloseElement();
        }

        internal void OnDragStartCore(MutableDragEventArgs e)
        {
            OnDragStart?.Invoke(Item, e);
        }

        public ValueTask DisposeAsync()
            => JSRuntime.InvokeVoidAsync("_blazorDragAndDrop.unregisterDrag", _id);
    }
}
