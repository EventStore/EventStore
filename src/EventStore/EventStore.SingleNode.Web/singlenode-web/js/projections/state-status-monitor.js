"use strict";
// projection monitor
define(["projections/url-monitor"], function (urlMonitor) {
    return function (baseUrl) {
        var stateMonitor = null;
        var statusMonitor = null;

        return {
            start: function (statusChanged, stateChanged) {
                stateMonitor = urlMonitor(baseUrl + "/state", "application/json", "text");
                statusMonitor = urlMonitor(baseUrl + "/statistics", "application/json");

                function statusChangedRaw(rawStatus) {
                    var status = rawStatus.projections[0];
                    statusChanged(status);
                }

                stateMonitor.start(stateChanged);
                statusMonitor.start(statusChangedRaw);
            },

            stop: function() {
                if (stateMonitor !== null) stateMonitor.stop();
                if (statusMonitor !== null) statusMonitor.stop();

                stateMonitor = null;
                statusMonitor = null;
            }
        };

    };
});