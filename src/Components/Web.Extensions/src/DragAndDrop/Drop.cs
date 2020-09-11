// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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

        // TODO: These could be turned into EventCallbacks.

        [Parameter]
        public Action<TItem, MutableDragEventArgs>? OnDrop { get; set; }

        [Parameter]
        public Action<TItem, MutableDragEventArgs>? OnDragOver { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object> AdditionalAttributes { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            var interopHandleReference = DotNetObjectReference.Create<object>(new DropInteropHandle<TItem>(this));
            _id = await JSRuntime.InvokeAsync<long>(DragAndDropInterop.RegisterDropHandle, interopHandleReference);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "ondrop", $"window.{DragAndDropInterop.OnDrop}(event, {_id})");
            builder.AddAttribute(2, "ondragover", $"window.{DragAndDropInterop.OnDragOver}(event, {_id})");
            builder.AddMultipleAttributes(3, AdditionalAttributes);
            builder.AddContent(4, ChildContent);
            builder.CloseElement();
        }

        internal bool CanDropCore(TItem item)
        {
            return CanDrop?.Invoke(item) ?? true;
        }

        internal void OnDropCore(TItem item, MutableDragEventArgs eventArgs)
        {
            OnDrop?.Invoke(item, eventArgs);
        }

        internal void OnDragOverCore(TItem item, MutableDragEventArgs eventArgs)
        {
            OnDragOver?.Invoke(item, eventArgs);
        }

        public ValueTask DisposeAsync()
            => JSRuntime.InvokeVoidAsync(DragAndDropInterop.UnregisterDropHandle, _id);
    }
}
