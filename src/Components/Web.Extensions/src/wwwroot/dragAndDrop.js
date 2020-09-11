(function () {
    // TODO: No need to round-trip event data if no event handler is specified in .NET. Find a way to get this information to JS.
    // TODO: Do we need some sort of event queueing mechanism? (force changes to be applied in the correct order?)
    // For example, onDragStart is async. onDragEnd could fire before onDragStart completes if the user is fast enough. We could extend
    // the current waiting mechanism to support multiple states (right now, it's used only to delay tasks until after onDragEnd completes).
    // TODO: Implement the rest of the drag events: https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API

    let nextDragHandleId = 0;
    let nextDropHandleId = 0;

    const dragHandlesById = {};
    const dropHandlesById = {};

    let activeDrag;
    let lastTargetDropHandle;

    function registerDragHandle(handle) {
        dragHandlesById[nextDragHandleId] = handle;
        return nextDragHandleId++;
    }

    async function unregisterDragHandle(id) {
        const handle = dragHandlesById[id];

        if (handle) {
            if (activeDrag && activeDrag.handle === handle) {
                // Don't want to dispose a drag reference when it's still being dragged
                await waitForLastDrag();
            }

            handle.dispose();
            delete dragHandlesById[id];
        }
    }

    function registerDropHandle(handle) {
        dropHandlesById[nextDropHandleId] = handle;
        return nextDropHandleId++;
    }

    function unregisterDropHandle(id) {
        const handle = dropHandlesById[id];

        if (handle) {
            handle.dispose();
            delete dropHandlesById[id];
        }
    }

    async function onDragStart(event, id) {
        const handle = getDragHandleByIdOrThrow(id);

        // TODO: This will wait forever if the onDragEnd event doesn't fire. I was able to get that to happen by
        // what seems to be a browser debugger glitch, so I'm not sure if it's a real scenario we have to cover.
        // If necessary, we could employ some sort of short timeout here.
        // It also looks like Firefox has a bug where dragend is not fired if the source node is moved or removed during
        // the drag. This would lead me to think we can't just leave this as-is.
        await waitForLastDrag();

        startNewDrag(handle);

        const initialData = getAllDataTransferData(event.dataTransfer);
        const dataTransferStore = await handle.invokeMethodAsync('OnDragStart', parseDragEvent(event), initialData);

        updateDataTransfer(event.dataTransfer, dataTransferStore);
    }

    async function onDragEnd(event) {
        if (!activeDrag) {
            return;
        }

        const initialData = getAllDataTransferData(event.dataTransfer);
        await activeDrag.handle.invokeMethodAsync('OnDragEnd', parseDragEvent(event), initialData, lastTargetDropHandle);

        lastTargetDropHandle = undefined;
        completeCurrentDrag();
    }

    async function onDrop(event, id) {
        event.preventDefault();

        if (!activeDrag) {
            return;
        }

        lastTargetDropHandle = getDropHandleByIdOrThrow(id);
        const initialData = getAllDataTransferData(event.dataTransfer);

        await lastTargetDropHandle.invokeMethodAsync('OnDrop', parseDragEvent(event), initialData, activeDrag.handle);
    }

    async function onDragOver(event, id) {
        // TODO: This event is likely called way more frequently than necessary: https://developer.mozilla.org/en-US/docs/Web/API/DragEvent
        // It would be nice to provide an option to throttle this callback.
        // Furthermore, the ability to avoid making JS->.NET calls if there is no user-provided callback would be a big win for this.

        event.preventDefault();

        if (!activeDrag) {
            return;
        }

        const dropHandle = getDropHandleByIdOrThrow(id);
        const initialData = getAllDataTransferData(event.dataTransfer);

        await dropHandle.invokeMethodAsync('OnDragOver', parseDragEvent(event), initialData, activeDrag.handle);
    }

    function getDropHandleByIdOrThrow(id) {
        const handle = dropHandlesById[id];

        if (handle) {
            return handle;
        }

        throw new Error(`Drop with ID ${id} does not exist.`);
    }

    function getDragHandleByIdOrThrow(id) {
        const handle = dragHandlesById[id];

        if (handle) {
            return handle;
        }

        throw new Error(`Drag with ID ${id} does not exist.`);
    }

    async function waitForLastDrag() {
        if (activeDrag) {
            await activeDrag.promise;
        }
    }

    function startNewDrag(handle) {
        activeDrag = {
            handle,
        };

        activeDrag.promise = new Promise(function (resolve) {
            activeDrag.complete = resolve;
        });
    }

    function completeCurrentDrag() {
        if (activeDrag) {
            activeDrag.complete();
            activeDrag = undefined;
        }
    }

    function parseDragEvent(event) {
        return {
            // Mouse event properties
            type: event.type,
            detail: event.detail,
            screenX: event.screenX,
            screenY: event.screenY,
            clientX: event.clientX,
            clientY: event.clientY,
            offsetX: event.offsetX,
            offsetY: event.offsetY,
            button: event.button,
            buttons: event.buttons,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey,
            altKey: event.altKey,
            metaKey: event.metaKey,

            // Drag event properties
            dataTransfer: {
                dropEffect: event.dataTransfer.dropEffect,
                effectAllowed: event.dataTransfer.effectAllowed,
                files: [...event.dataTransfer.files],
                items: [...event.dataTransfer.items],
                types: event.dataTransfer.types,
            },
        }
    }

    function updateDataTransfer(dataTransfer, dataTransferStore) {
        dataTransfer.clearData();

        Object.entries(dataTransferStore.data).forEach(function ([format, data]) {
            dataTransfer.setData(format, data);
        });

        if (dataTransferStore.dragImage) {
            dataTransfer.setDragImage(dataTransferStore.dragImage, dataTransferStore.drageImageXOffset, dataTransferStore.dragImageYOffset);
        }

        dataTransfer.dropEffect = dataTransferStore.dataTransfer.dropEffect;
        dataTransfer.effectAllowed = dataTransferStore.dataTransfer.effectAllowed;
    }

    function getAllDataTransferData(dataTransfer) {
        const data = {};

        dataTransfer.types.forEach(function (type) {
            data[type] = dataTransfer.getData(type);
        });

        return data;
    }

    window._blazorDragAndDrop = {
        registerDragHandle,
        unregisterDragHandle,
        registerDropHandle,
        unregisterDropHandle,
        onDragStart,
        onDragEnd,
        onDrop,
        onDragOver,
    };
})();
