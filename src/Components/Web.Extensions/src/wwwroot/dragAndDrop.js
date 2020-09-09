(function () {
    // TODO: Abstract away duplicate component tracking code

    let nextDragId = 0;
    let nextDropId = 0;

    const dragsById = {};
    const dropsById = {};

    function registerDrag(dragObjectReference) {
        dragsById[nextDragId] = dragObjectReference;
        return nextDragId++;
    }

    function unregisterDrag(dragId) {
        const drag = dragsById[dropId];

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

    async function onDrop(event, dropId) {
        event.preventDefault();

        const drop = getDropByIdOrThrow(dropId);
        await drop.invokeMethodAsync("OnDrop");
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

    window._blazorDragAndDrop = {
        registerDrag,
        unregisterDrag,
        registerDrop,
        unregisterDrop,
        onDrop,
        onDragOver,
    };
})();
