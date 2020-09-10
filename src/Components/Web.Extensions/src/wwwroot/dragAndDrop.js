(function () {
    // TODO: No need to round-trip event data if no event handler is specified in .NET. Find a way to get this information to JS.
    // TODO: Test with multiple items being dragged at once.
    // TODO: Do we need some sort of event queueing mechanism? (force changes to be applied in the correct order?)

    // TODO: handle multiple drag items.
    // Then, implement the missing features, e.g. updateDataTransfer

    let nextDragId = 0;
    let nextDropId = 0;

    const dragsById = {};
    const dropsById = {};

    const dragStatesById = {};

    function registerDrag(dragObjectReference) {
        dragsById[nextDragId] = dragObjectReference;
        return nextDragId++;
    }

    async function unregisterDrag(dragId) {
        const drag = dragsById[dragId];

        if (drag) {
            const dragState = dragStatesById[dragId];

            if (dragState) {
                // Drag has not yet completed - wait for it to complete before disposing.
                await dragState.promise;
            }

            drag.dispose();
            delete dragsById[dragId];
        }
    }

    function registerDrop(dropObjectReference) {
        dropsById[nextDropId] = dropObjectReference;
        return nextDropId++;
    }

    function unregisterDrop(dropId) {
        const drop = dropsById[dropId];

        if (drop) {
            drop.dispose();
            delete dropsById[dropId];
        }
    }

    async function onDragStart(event, dragId) {
        const drag = getDragByIdOrThrow(dragId);

        const dragState = {};
        dragState.promise = new Promise(function (resolve) {
            dragState.resolve = resolve;
        });

        dragStatesById[dragId] = dragState;

        const initialData = getAllDataTransferData(event.dataTransfer);
        const dataTransferStore = await drag.invokeMethodAsync('OnDragStart', parseDragEvent(event), initialData);

        console.log(dataTransferStore);

        updateDataTransfer(event.dataTransfer, dataTransferStore);
    }

    async function onDragEnd(event, dragId) {
        // TODO: Identify drop target and provide that info to the .NET callback.
        const drag = getDragByIdOrThrow(dragId);

        const initialData = getAllDataTransferData(event.dataTransfer);
        await drag.invokeMethodAsync('OnDragEnd', parseDragEvent(event), initialData);

        const unregistrationDeferrer = dragStatesById[dragId];

        if (unregistrationDeferrer) {
            unregistrationDeferrer.resolve();
            delete dragStatesById[dragId];
        }

        console.log(Object.keys(dragStatesById).length);
    }

    async function onDrop(event, dropId) {
        event.preventDefault();

        const initialData = getAllDataTransferData(event.dataTransfer);
        const drop = getDropByIdOrThrow(dropId);
        const activeDrags = Object.keys(dragStatesById).map(id => getDragByIdOrThrow(id));
        await drop.invokeMethodAsync('OnDrop', parseDragEvent(event), initialData, activeDrags);
    }

    function onDragOver(event, dropId) {
        // TODO
        event.preventDefault();
    }

    function getDropByIdOrThrow(dropId) {
        const drop = dropsById[dropId];

        if (drop) {
            return drop;
        }

        throw new Error(`Drop with ID ${dropId} does not exist.`);
    }

    function getDragByIdOrThrow(dragId) {
        const drag = dragsById[dragId];

        if (drag) {
            return drag;
        }

        throw new Error(`Drag with ID ${dragId} does not exist.`);
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
            dataTransfer: event.dataTransfer,
        }
    }

    function updateDataTransfer(dataTransfer, dataTransferStore) {
        // TODO: Update drop effect, etc.

        dataTransfer.clearData();

        Object.entries(dataTransferStore.data).forEach(function ([format, data]) {
            dataTransfer.setData(format, data);
        });

        if (dataTransferStore.dragImage) {
            dataTransfer.setDragImage(dataTransferStore.dragImage, dataTransferStore.drageImageXOffset, dataTransferStore.dragImageYOffset);
        }
    }

    function getAllDataTransferData(dataTransfer) {
        const data = {};

        dataTransfer.types.forEach(function (type) {
            data[type] = dataTransfer.getData(type);
        });

        return data;
    }

    window._blazorDragAndDrop = {
        registerDrag,
        unregisterDrag,
        registerDrop,
        unregisterDrop,
        onDragStart,
        onDragEnd,
        onDrop,
        onDragOver,
    };
})();
