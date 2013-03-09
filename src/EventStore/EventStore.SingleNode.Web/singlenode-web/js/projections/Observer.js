"use strict";
// projection monitor
define(["projections/ResourceMonitor"], function (resourceMonitor) {
    //TODO: handle errors
    return {
        create: function (baseUrl) {
            var stateMonitor = null;
            var resultMonitor = null;
            var statusMonitor = null;
            var sourceMonitor = null;
            var commandErrorHandler = [];
            var pendingSubscribe = [];
            var subscribed = { statusChanged: [], stateChanged: [], resultChanged: [], sourceChanged: [], error: []};

            function enrichStatus(status) {
                var startUpdateAvailable =
                    status.status.indexOf("Loaded") === 0 ||
                        status.status.indexOf("Stopped") === 0 ||
                        status.status.indexOf("Completed") === 0 ||
                        status.status.indexOf("Faulted") === 0;
                status.availableCommands = {
                    stop: status.status.indexOf("Running") === 0,
                    start:startUpdateAvailable,
                    update: startUpdateAvailable,
                    debug: status.status.indexOf("Faulted") === 0,
                };
                return status;
            }

            function dispatch(handlers, arg) {
                for (var i = 0; i < handlers.length; i++) 
                    handlers[i](arg);
            }

            function internalSubscribe(handlers) {

                stateMonitor = resourceMonitor.create(baseUrl + "/state", "application/json", "text");
                resultMonitor = resourceMonitor.create(baseUrl + "/result", "application/json", "text");
                statusMonitor = resourceMonitor.create(baseUrl + "/statistics", "application/json");
                sourceMonitor = resourceMonitor.create(baseUrl + "/query?config=yes", "application/json");

                if (handlers.statusChanged) {
                    if (subscribed.statusChanged.length == 0) 
                        statusMonitor.start(function(rawStatus) {
                            var status = rawStatus.projections[0];
                            var enriched = enrichStatus(status);
                            dispatch(subscribed.statusChanged, enriched);
                        });
                    subscribed.statusChanged.push(handlers.statusChanged);
                }

                if (handlers.stateChanged) {
                    if (subscribed.stateChanged.length == 0)
                        stateMonitor.start(function (v) { dispatch(subscribed.stateChanged, v); });
                    subscribed.stateChanged.push(handlers.stateChanged);
                }

                if (handlers.resultChanged) {
                    if (subscribed.resultChanged.length == 0)
                        resultMonitor.start(function (v) { dispatch(subscribed.resultChanged, v); });
                    subscribed.resultChanged.push(handlers.resultChanged);
                }

                if (handlers.sourceChanged) {
                    if (subscribed.sourceChanged.length == 0)
                        sourceMonitor.start(function (v) { dispatch(subscribed.sourceChanged, v); });
                    subscribed.sourceChanged.push(handlers.sourceChanged);
                }


                if (handlers.error) {
                    commandErrorHandler.push(handlers.error);
                }
            }

            return {
                subscribe: function (handlers) {
                    if (baseUrl) {
                        internalSubscribe(handlers);
                    } else {
                        pendingSubscribe.push(handlers);
                    }
                },

                poll: function () {
                    if (stateMonitor) stateMonitor.poll();
                    if (resultMonitor) resultMonitor.poll();
                    if (statusMonitor) statusMonitor.poll();
                    if (sourceMonitor) sourceMonitor.poll();
                },

                configureUrl: function (url) {
                    baseUrl = url;
                    if (pendingSubscribe)
                        for (var i = 0; i < pendingSubscribe.length; i++) 
                            internalSubscribe(pendingSubscribe[i]);
                    pendingSubscribe = [];
                }

            };
        }
    };
});