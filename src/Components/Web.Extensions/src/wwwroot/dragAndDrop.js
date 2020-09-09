(function () {
    // TODO: Abstract away duplicate component tracking code.
    // TODO: No need to round-trip event data if no event handler is specified in .NET. Find a way to get this information to JS.

    let nextDragId = 0;
    let nextDropId = 0;

    const dragsById = {};
    const dropsById = {};

    const activeDrags = [];

    function registerDrag(dragObjectReference) {
        dragsById[nextDragId] = dragObjectReference;
        return nextDragId++;
    }

    function unregisterDrag(dragId) {
        const drag = dragsById[dragId];

        if (drag) {
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

    // TODO: If lifecycle problems become a concern (e.g. a Drag is disposed while the drag is happening),
    // you can track a separate object holding the dragged item in .NET (maybe in DragInteropRelay?)
    async function onDragStart(event, dragId) {
        const drag = getDragByIdOrThrow(dragId);

        activeDrags.push(drag);

        const initialData = getAllDataTransferData(event.dataTransfer);
        const dataTransferStore = await drag.invokeMethodAsync('OnDragStart', parseDragEvent(event), initialData);

        console.log(dataTransferStore);

        updateDataTransfer(event.dataTransfer, dataTransferStore);
    }

    async function onDrop(event, dropId) {
        event.preventDefault();

        const initialData = getAllDataTransferData(event.dataTransfer);
        const drop = getDropByIdOrThrow(dropId);
        await drop.invokeMethodAsync('OnDrop', parseDragEvent(event), initialData, activeDrags);

        // TODO: Async concerns? (copy activeDrags, clear, then invoke async method?)

        activeDrags.length = 0;
    }

    function onDragOver(event, dropId) {
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
        dataTransfer.clearData();

        Object.entries(dataTransferStore.data).forEach(([format, data]) => {
            dataTransfer.setData(format, data);
        });

        if (dataTransferStore.dragImage) {
            dataTransfer.setDragImage(dataTransferStore.dragImage, dataTransferStore.drageImageXOffset, dataTransferStore.dragImageYOffset);
        }
    }

    function getAllDataTransferData(dataTransfer) {
        const data = {};

        dataTransfer.types.forEach((type) => {
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
        onDrop,
        onDragOver,
    };
})();
