mergeInto(LibraryManager.library, {

  sendMessageToWeb: function (message) {
    onDataRecieved(message);
  },
  sendErrorToWeb: function (message) {
    handleWebglError(message);
  }
})