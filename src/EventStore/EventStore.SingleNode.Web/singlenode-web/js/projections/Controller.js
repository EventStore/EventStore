"use strict";
// projection monitor
define(["projections/ResourceMonitor"], function (resourceMonitor) {
    //TODO: handle errors
    return {
        create: function createController(baseUrl, observer) {
            var commandErrorHandler = null;

            function postCommand(command) {
                $.ajax(baseUrl + "/command/" + command, {
                    headers: {
                        Accept: "application/json",
                    },
                    type: "POST",
                    success: successPostCommand,
                    error: errorPostCommand,
                });
            }

            function postSource(source, emit) {
                var params = $.param({
                    emit: emit ? "yes" : "no",
                });
                $.ajax(baseUrl + "/query?" + params, {
                    headers: {
                        Accept: "application/json",
                    },

                    type: "PUT",
                    data: source,
                    success: successPostCommand,
                    error: errorPostCommand,
                });
            }

            function successPostCommand(data, status, xhr) {
                if (observer)
                    observer.poll();
            }

            function errorPostCommand(xhr, status, error) {
                if (observer)
                    observer.poll();
            }

            return {
                start: function () {
                    postCommand("enable");
                },
                stop: function() {
                    postCommand("disable");
                },
                update: function (query, emit) {
                    postSource(query, emit);
                }
            };
        }
    };
});