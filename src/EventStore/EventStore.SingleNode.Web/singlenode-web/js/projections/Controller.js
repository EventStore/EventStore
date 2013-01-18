"use strict";
// projection monitor
define(function () {
    //TODO: handle errors
    return {
        create: function (baseUrl, observer) {
            var commandErrorHandler = null;

            function postCommand(command, success) {
                $.ajax(baseUrl + "/command/" + command, {
                    headers: {
                        Accept: "application/json",
                    },
                    type: "POST",
                    success: successPostCommand(success),
                    error: errorPostCommand,
                });
            }

            function postSource(source, emit, success) {
                var params = $.param({
                    emit: emit ? "yes" : "no",
                });
                $.ajax(baseUrl + "/query?" + params, {
                    headers: {
                        Accept: "application/json",
                    },

                    type: "PUT",
                    data: source,
                    success: successPostCommand(success),
                    error: errorPostCommand,
                });
            }

            function successPostCommand(success) {
                return function (data, status, xhr) {
                    if (success)
                        success();
                    if (observer)
                        observer.poll();
                };
            }

            function errorPostCommand(xhr, status, error) {
                if (observer)
                    observer.poll();
            }

            return {
                start: function (success) {
                    postCommand("enable", success);
                },
                stop: function (success) {
                    postCommand("disable", success);
                },
                update: function (query, emit, success) {
                    postSource(query, emit, success);
                }
            };
        }
    };
});