// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Microsoft.AspNetCore.Components.Web.Extensions
{
    public class Drop<TItem> : ComponentBase, IAsyncDisposable
    {
        private long _id;

        [Inject]
        private IJSRuntime JSRuntime { get; set; } = default!;

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public Func<TItem, bool>? CanDrop { get; set; }

        [Parameter]
        public Action<DropInfo<TItem>>? OnDrop { get; set; }

        protected override async Task OnInitializedAsync()
        {
            var interopRelayReference = DotNetObjectReference.Create(new DropInteropRelay<TItem>(this));
            _id = await JSRuntime.InvokeAsync<long>("_blazorDragAndDrop.registerDrop", interopRelayReference);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "ondrop", $"window._blazorDragAndDrop.onDrop(event, {_id})");
            builder.AddAttribute(2, "ondragover", $"window._blazorDragAndDrop.onDragOver(event, {_id})");
            builder.AddContent(3, ChildContent);
            builder.CloseElement();
        }

        internal bool CanDropCore(TItem item)
        {
            return CanDrop?.Invoke(item) ?? true;
        }

        internal void OnDropCore(MutableDragEventArgs eventArgs, TItem[] items)
        {
            OnDrop?.Invoke(new DropInfo<TItem>(eventArgs, items));
        }

        public ValueTask DisposeAsync()
            => JSRuntime.InvokeVoidAsync("_blazorDragAndDrop.unregisterDrop", _id);
    }
}
