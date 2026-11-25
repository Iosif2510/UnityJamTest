// A browser may refuse to lock the mouse (the page is not focused yet, it sits in another site's frame, or the
// request came outside a click). Unity reports the refusal as a fatal-looking "An error occurred" box. This makes
// a refusal quiet: the storeroom then simply asks the player to click the screen, which locks it properly.
mergeInto(LibraryManager.library, {
  PotionGuardPointerLock: function () {
    if (window.__potionPointerLockGuard) return;
    window.__potionPointerLockGuard = true;
    var original = Element.prototype.requestPointerLock;
    if (!original) return;
    Element.prototype.requestPointerLock = function () {
      try {
        var result = original.apply(this, arguments);
        if (result && typeof result.catch === "function")
          result.catch(function (e) { console.warn("Pointer lock refused: " + (e && e.name)); });
        return result;
      } catch (e) {
        console.warn("Pointer lock refused: " + (e && e.name));
      }
    };
  }
});
