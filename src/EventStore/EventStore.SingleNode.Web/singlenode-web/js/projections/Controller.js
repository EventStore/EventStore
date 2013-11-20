"use strict";
// projection monitor
define(function () {
    //TODO: handle errors

    function internalCreate(mode, url, observer) {
        var baseUrl = url;
        var created = (mode === "projection") || (mode === "query" && url);
        var commandErrorHandler = null;

        if (observer)
            observer.subscribe({
                urlChanged: function(newBaseUrl) {
                    if (!newBaseUrl) {
                        created = false;
                        baseUrl = null;
                    } else {
                        created = true;
                        baseUrl = newBaseUrl;
                    }
                }
            });

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

        function postNew(source, emit, success) {

            var params = $.param({
                emit: "no",
                checkpoints: "no",
                enabled: "no",
            });

            var url = "/projections/transient?" + params;

            $.ajax(url, {
                headers: {
                    Accept: "application/json",
                },
                data: source,
                type: 'POST',
                success: function (data, textStatus, jqXHR) {
                    created = true;
                    baseUrl = jqXHR.getResponseHeader('Location');
                    if (observer) 
                        observer.configureUrl(baseUrl);
                    successPostCommand(success)(data, textStatus, jqXHR);
                },
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
                if (created)
                    postCommand("enable", success);
            },
            stop: function (success) {
                if (created)
                    postCommand("disable", success);
            },
            reset: function(success) {
                if (created)
                    postCommand("reset", success);
            },
            update: function (query, emit, success) {
                if (created)
                    postSource(query, emit, success);
                else
                    postNew(query, emit, success);
            }
        };
    }

    return {
        create: function(url, observer) {
            return internalCreate("projection", url, observer);
        },
        createQuery: function(observer) {
            return internalCreate("query", null, observer);
        },
        openQuery: function(url, observer) {
            return internalCreate("query", url, observer);
        }
    };
});