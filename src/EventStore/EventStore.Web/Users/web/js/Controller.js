"use strict";
// projection monitor
define(function () {
    //TODO: handle errors

    function internalCreate() {

        function postCommand(command, data, success) {
            $.ajax("/users/" + command.endpoint, {
                headers: {
                    Accept: "application/json",
                },
                contentType: "application/json",
                dataType: "json",
                type: command.method,
                data: JSON.stringify(data),
                success: successPostCommand(success),
                error: errorPostCommand(),
            });
        }

        function get(loginName, success) {
            $.ajax("/users/" + encodeURIComponent(loginName), {
                headers: {
                    Accept: "application/json",
                },
                dataType: "json",
                type: "GET",
                success: successGet(success),
                error: errorGet(),
            });
        }

        function successPostCommand(success) {
            return function (data, status, xhr) {
                if (success)
                    success();
            };
        }

        function errorPostCommand(xhr, status, error) {
            return function (xhr, data, status) {
                reportError(data, status, xhr);
            };
        }

        function successGet(success) {
            return function (data, status, xhr) {
                if (success)
                    success(data);
            };
        }

        function errorGet() {
            return function (xhr, data, status) {
                reportError(data, status, xhr);
            };
        }

        function reportError(data, status, xhr) {
            alert("FAILED: " + status);
        }

        return {
            create: function (data, success) {
                postCommand({ method: "POST", endpoint: "" }, data, success);
            },
            update: function (data, success) {
                postCommand({ method: "PUT", endpoint: "" }, data, success);
            },
            enable: function (data, success) {
                postCommand({ method: "POST", endpoint: data.loginName + "/enable" }, data, success);
            },
            disable: function (data, success) {
                postCommand({ method: "POST", endpoint: data.disableName + "/enable" }, data, success);
            },
            setPassword: function(data, success) {
                postCommand({ method: "POST", endpoint: data.loginName + "/password" }, data, success);
            },
            get: function (loginName, success) {
                get(loginName, success);
            }
        };
    }

    return {
        create: function () {
            return internalCreate();
        },
    };
});