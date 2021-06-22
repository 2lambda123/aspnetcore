export const DragAndDrop = {
  registerDragDotNetHelper,
  unregisterDragDotNetHelper,
  registerDropDotNetHelper,
  unregisterDropDotNetHelper,
  onDragStart,
  onDragEnd,
  onDrop,
  onDragOver,
};

// TODO: No need to round-trip event data if no event handler is specified in .NET. Find a way to get this information to JS.
// TODO: Do we need some sort of event queueing mechanism? (force changes to be applied in the correct order?)
// For example, onDragStart is async. onDragEnd could fire before onDragStart completes if the user is fast enough. We could extend
// the current waiting mechanism to support multiple states (right now, it's used only to delay tasks until after onDragEnd completes).
// TODO: Implement the rest of the drag events: https://developer.mozilla.org/en-US/docs/Web/API/HTML_Drag_and_Drop_API

let nextDragDotNetHelperId = 0;
let nextDropDotNetHelperId = 0;

const dragDotNetHelpersById = {};
const dropDotNetHelpersById = {};

class Drag {
  dotNetHelper: any;

  promise: Promise<void> | undefined;

  complete?(value: void | PromiseLike<void>): void;

  constructor(dotNetHelper: any) {
    this.dotNetHelper = dotNetHelper;
    this.promise = new Promise((resolve) => {
      this.complete = resolve;
    });
  }
}

let activeDrag: Drag | undefined;
let lastTargetDropDotNetHelper: any;

function registerDragDotNetHelper(dotNetHelper: any): number {
  dragDotNetHelpersById[nextDragDotNetHelperId] = dotNetHelper;
  return nextDragDotNetHelperId++;
}

async function unregisterDragDotNetHelper(id: number): Promise<void> {
  const dotNetHelper = dragDotNetHelpersById[id];

  if (dotNetHelper) {
    if (activeDrag && activeDrag.dotNetHelper === dotNetHelper) {
      // Don't want to dispose a drag reference when it's still being dragged
      await waitForLastDrag();
    }

    dotNetHelper.dispose();
    delete dragDotNetHelpersById[id];
  }
}

function registerDropDotNetHelper(dotNetHelper: any): number {
  dropDotNetHelpersById[nextDropDotNetHelperId] = dotNetHelper;
  return nextDropDotNetHelperId++;
}

function unregisterDropDotNetHelper(id: number): void {
  const dotNetHelper = dropDotNetHelpersById[id];

  if (dotNetHelper) {
    dotNetHelper.dispose();
    delete dropDotNetHelpersById[id];
  }
}

async function onDragStart(event: DragEvent, id: number): Promise<void> {
  const dotNetHelper = getDragDotNetHelperByIdOrThrow(id);

  // TODO: This will wait forever if the onDragEnd event doesn't fire. I was able to get that to happen by
  // what seems to be a browser debugger glitch, so I'm not sure if it's a real scenario we have to cover.
  // If necessary, we could employ some sort of short timeout here.
  // It also looks like Firefox has a bug where dragend is not fired if the source node is moved or removed during
  // the drag. This would lead me to think we can't just leave this as-is.
  await waitForLastDrag();

  startNewDrag(dotNetHelper);

  const initialData = getAllDataTransferData(event.dataTransfer);
  const dataTransferStore = await dotNetHelper.invokeMethodAsync('OnDragStart', parseDragEvent(event), initialData);

  updateDataTransfer(event.dataTransfer, dataTransferStore);
}

async function onDragEnd(event: DragEvent): Promise<void> {
  if (!activeDrag) {
    return;
  }

  const initialData = getAllDataTransferData(event.dataTransfer);
  await activeDrag.dotNetHelper.invokeMethodAsync('OnDragEnd', parseDragEvent(event), initialData, lastTargetDropDotNetHelper);

  lastTargetDropDotNetHelper = undefined;
  completeCurrentDrag();
}

async function onDrop(event: DragEvent, id: number): Promise<void> {
  event.preventDefault();

  if (!activeDrag) {
    return;
  }

  lastTargetDropDotNetHelper = getDropDotNetHelperByIdOrThrow(id);
  const initialData = getAllDataTransferData(event.dataTransfer);

  await lastTargetDropDotNetHelper.invokeMethodAsync('OnDrop', parseDragEvent(event), initialData, activeDrag.dotNetHelper);
}

async function onDragOver(event: DragEvent, id: number): Promise<void> {
  // TODO: This event is likely called way more frequently than necessary: https://developer.mozilla.org/en-US/docs/Web/API/DragEvent
  // It would be nice to provide an option to throttle this callback.
  // Furthermore, the ability to avoid making JS->.NET calls if there is no user-provided callback would be a big win for this.

  event.preventDefault();

  if (!activeDrag) {
    return;
  }

  const dotNetHelper = getDropDotNetHelperByIdOrThrow(id);
  const initialData = getAllDataTransferData(event.dataTransfer);

  await dotNetHelper.invokeMethodAsync('OnDragOver', parseDragEvent(event), initialData, activeDrag.dotNetHelper);
}

function getDropDotNetHelperByIdOrThrow(id) {
  const dotNetHelper = dropDotNetHelpersById[id];

  if (dotNetHelper) {
    return dotNetHelper;
  }

  throw new Error(`Drop with ID ${id} does not exist.`);
}

function getDragDotNetHelperByIdOrThrow(id) {
  const dotNetHelper = dragDotNetHelpersById[id];

  if (dotNetHelper) {
    return dotNetHelper;
  }

  throw new Error(`Drag with ID ${id} does not exist.`);
}

async function waitForLastDrag() {
  if (activeDrag) {
    await activeDrag.promise;
  }
}

function startNewDrag(dotNetHelper: any) {
  activeDrag = new Drag(dotNetHelper);
}

function completeCurrentDrag() {
  if (activeDrag !== undefined) {
    // TODO: Use classes and give Drag a constructor.
    if (activeDrag.complete !== undefined) {
      activeDrag.complete();
    }
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
  };
}

function updateDataTransfer(dataTransfer, dataTransferStore) {
  dataTransfer.clearData();

  Object.entries(dataTransferStore.data).forEach(function([format, data]) {
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

  dataTransfer.types.forEach(function(type) {
    data[type] = dataTransfer.getData(type);
  });

  return data;
}
