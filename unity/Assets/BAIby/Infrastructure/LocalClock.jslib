// Backs LocalClock on the Web build, where the .NET time zone is UTC.
mergeInto(LibraryManager.library, {
  // Minutes east of UTC for the browser's current local time.
  BAIbyLocalOffsetMinutes: function () {
    return -Math.round(new Date().getTimezoneOffset());
  },
});
