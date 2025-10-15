(function () {
    const LAST_MEDIA_KEY = "lastMediaId";
    const ZOOM_BANNER_KEY = "zoomBannerDismissedAt";
    const EIGHTEEN_HOURS_MS = 18 * 60 * 60 * 1000;

    function nowUtc() {
        return new Date().getTime();
    }

    window.myShowtimeState = {
        getLastMediaId: function () {
            const stored = window.localStorage.getItem(LAST_MEDIA_KEY);
            return stored && stored.length > 0 ? stored : null;
        },

        setLastMediaId: function (id) {
            if (typeof id !== "string" || id.length === 0) {
                window.localStorage.removeItem(LAST_MEDIA_KEY);
                return;
            }
            window.localStorage.setItem(LAST_MEDIA_KEY, id);
        },

        clearLastMediaId: function () {
            window.localStorage.removeItem(LAST_MEDIA_KEY);
        },

        shouldShowZoomBanner: function () {
            const stored = window.localStorage.getItem(ZOOM_BANNER_KEY);
            if (!stored) {
                return true;
            }
            const dismissedAt = Number.parseInt(stored, 10);
            if (!Number.isFinite(dismissedAt)) {
                return true;
            }
            return nowUtc() - dismissedAt >= EIGHTEEN_HOURS_MS;
        },

        markZoomBannerDismissed: function () {
            window.localStorage.setItem(ZOOM_BANNER_KEY, String(nowUtc()));
        }
    };
})();
