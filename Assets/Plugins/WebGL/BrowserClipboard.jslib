// Copying text in a browser. The page may only write the clipboard while handling the player's own click, and
// Unity runs its button callbacks a frame later, outside that click. So the text is handed over on the pointer
// press (PotionArmCopy) and written when the browser reports the release of that same press.
mergeInto(LibraryManager.library, {
  PotionArmCopy: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var copy = function () {
      document.removeEventListener("pointerup", copy, true);
      document.removeEventListener("touchend", copy, true);
      var done = false;
      try {
        var area = document.createElement("textarea");
        area.value = text;
        area.setAttribute("readonly", "");
        area.style.position = "fixed";
        area.style.opacity = "0";
        document.body.appendChild(area);
        area.select();
        done = document.execCommand("copy");
        document.body.removeChild(area);
      } catch (e) { }
      if (!done && navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).catch(function () { });
      }
    };
    document.addEventListener("pointerup", copy, true);
    document.addEventListener("touchend", copy, true);
  }
});
