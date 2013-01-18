"use strict";
// projection monitor
define(["projections/ResourceMonitor"], function (resourceMonitor) {
    //TODO: handle errors
    return {
        create: function (baseUrl) {
            var stateMonitor = null;
            var statusMonitor = null;
            var sourceMonitor = null;
            var commandErrorHandler = null;

            function enrichStatus(status) {
                status.availableCommands = {
                    stop: status.status.indexOf("Running") === 0,
                    start:
                        status.status.indexOf("Loaded") === 0 ||
                        status.status.indexOf("Stopped") === 0 ||
                        status.status.indexOf("Completed") === 0 ||
                        status.status.indexOf("Faulted") === 0,
                    update: true,
                };
                return status;
            }

            return {
                subscribe: function(handlers) {

                    stateMonitor = resourceMonitor.create(baseUrl + "/state", "application/json", "text");
                    statusMonitor = resourceMonitor.create(baseUrl + "/statistics", "application/json");
                    sourceMonitor = resourceMonitor.create(baseUrl + "/query?config=yes", "application/json");

                    if (handlers.statusChanged) {
                        statusMonitor.start(function(rawStatus) {
                            var status = rawStatus.projections[0];
                            var enriched = enrichStatus(status);
                            handlers.statusChanged(enriched);
                        });
                    }

                    if (handlers.stateChanged) {
                        stateMonitor.start(handlers.stateChanged);
                    }

                    if (handlers.sourceChanged) {
                        sourceMonitor.start(handlers.sourceChanged);
                    }


                    if (handlers.error) {
                        commandErrorHandler = handlers.error;
                    }
                },

                unsubscribe: function() {
                    if (stateMonitor !== null) stateMonitor.stop();
                    if (statusMonitor !== null) statusMonitor.stop();

                    stateMonitor = null;
                    statusMonitor = null;
                },

                poll: function () {
                    stateMonitor.poll();
                    statusMonitor.poll();
                    sourceMonitor.poll();
                },

            };
        }
    };
});