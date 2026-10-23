// RunAnalytics.cs: sends one analytics row with navigator.sendBeacon. A text/plain beacon needs no CORS preflight, is never
// read back, and still goes out while the page is closing. Returns 1 when the browser queued it, 0 to let C# fall back.
mergeInto(LibraryManager.library, {
  WarpAnalyticsSend: function (url, body) {
    try {
      if (typeof navigator !== "undefined" && navigator.sendBeacon &&
          navigator.sendBeacon(UTF8ToString(url), new Blob([UTF8ToString(body)], { type: "text/plain" }))) return 1;
    } catch (e) { }
    return 0;
  }
});
