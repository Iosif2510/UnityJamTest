// The browser's IANA time zone (e.g. "Asia/Seoul"), for picking a nearby Relay region without measuring
// (OnlineRelayRegion.cs). Returns an empty string if the browser does not say.
mergeInto(LibraryManager.library, {
  PotionBrowserTimeZone: function () {
    var zone = "";
    try { zone = Intl.DateTimeFormat().resolvedOptions().timeZone || ""; } catch (e) { }
    var size = lengthBytesUTF8(zone) + 1;
    var buffer = _malloc(size);
    stringToUTF8(zone, buffer, size);
    return buffer;
  }
});
