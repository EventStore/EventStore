"use strict";
// projection monitor
define(["projections/ResourceMonitor"], function(resourceMonitor) {
    //TODO: handle errors
    return {
        create: function(baseUrl, options) {
            var autoRefresh = !options || options.autoRefresh;
            var stateMonitor = null;
            var resultMonitor = null;
            var statusMonitor = null;
            var sourceMonitor = null;
            var commandErrorHandler = [];
            var pendingSubscribe = [];
            var subscribed = { urlChanged: [], statusChanged: [], stateChanged: [], resultChanged: [], sourceChanged: [], error: [] };

            function enrichStatus(status) {
                var startUpdateAvailable =
                    status.status.indexOf("Loaded") === 0 ||
                        status.status.indexOf("Stopped") === 0 ||
                        status.status.indexOf("Completed") === 0 ||
                        status.status.indexOf("Faulted") === 0;
                var stopAvailable = status.status.indexOf("Running") === 0
                    || status.status.indexOf("Faulted") === 0
                    || (status.status.indexOf("Stopped") === 0 && status.status.indexOf("Enabled") > 0);
                status.availableCommands = {
                    stop: stopAvailable,
                    start: startUpdateAvailable,
                    update: startUpdateAvailable,
                    debug:
                        status.status.indexOf("Loaded") === 0 ||
                            status.status.indexOf("Stopped") === 0 ||
                            status.status.indexOf("Completed") === 0 ||
                            status.status.indexOf("Faulted") === 0,
                };
                return status;
            }

            function dispatch(handlers, arg1, arg2) {
                for (var i = 0; i < handlers.length; i++)
                    handlers[i](arg1, arg2);
            }

            function transientErrorHandler() {
            }

            function goneHandler() {
                if (stateMonitor)
                    stateMonitor.stop();
                if (resultMonitor)
                    resultMonitor.stop();
                if (statusMonitor)
                    statusMonitor.stop();
                if (sourceMonitor)
                    sourceMonitor.stop();

                stateMonitor = null;
                resultMonitor = null;
                statusMonitor = null;
                sourceMonitor = null;

                configureUrl(null);
            }

            function internalSubscribe(handlers) {

                stateMonitor = resourceMonitor.create(baseUrl + "/state", { accept: "application/json", type: "text", autoRefresh: autoRefresh });
                resultMonitor = resourceMonitor.create(baseUrl + "/result", { accept: "application/json", type: "text", autoRefresh: autoRefresh });
                statusMonitor = resourceMonitor.create(baseUrl + "/statistics", { accept: "application/json", autoRefresh: autoRefresh });
                sourceMonitor = resourceMonitor.create(baseUrl + "/query?config=yes", { accept: "application/json", autoRefresh: autoRefresh });

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
                        stateMonitor.start(function (v, xhr) { dispatch(subscribed.stateChanged, v, xhr); },
                            transientErrorHandler, goneHandler);
                    subscribed.stateChanged.push(handlers.stateChanged);
                }

                if (handlers.resultChanged) {
                    if (subscribed.resultChanged.length == 0)
                        resultMonitor.start(function (v, xhr) { dispatch(subscribed.resultChanged, v, xhr); },
                            transientErrorHandler, goneHandler);
                    subscribed.resultChanged.push(handlers.resultChanged);
                }

                if (handlers.sourceChanged) {
                    if (subscribed.sourceChanged.length == 0)
                        sourceMonitor.start(function (v) { dispatch(subscribed.sourceChanged, v); },
                            transientErrorHandler, goneHandler);
                    subscribed.sourceChanged.push(handlers.sourceChanged);
                }


                if (handlers.error) {
                    commandErrorHandler.push(handlers.error);
                }
            }


            function configureUrl(url) {
                baseUrl = url;
                if (baseUrl) {
                    if (pendingSubscribe)
                        for (var i = 0; i < pendingSubscribe.length; i++)
                            internalSubscribe(pendingSubscribe[i]);
                    pendingSubscribe = [];
                }
                dispatch(subscribed.urlChanged, baseUrl);
            }

            return {
                subscribe: function(handlers) {
                    if (handlers.urlChanged) 
                        subscribed.urlChanged.push(handlers.urlChanged);

                    if (baseUrl) {
                        internalSubscribe(handlers);
                    } else {
                        pendingSubscribe.push(handlers);
                    }
                },

                poll: function() {
                    if (stateMonitor) stateMonitor.poll();
                    if (resultMonitor) resultMonitor.poll();
                    if (statusMonitor) statusMonitor.poll();
                    if (sourceMonitor) sourceMonitor.poll();
                },

                configureUrl: configureUrl,
            };
        }
    };
});