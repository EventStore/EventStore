"use strict";
// projection monitor
define(function () {
    //TODO: handle errors

    function internalCreate() {

        function postCommand(command, data, success, error, login) {
            $.ajax("/users/" + command.endpoint, {
                headers: {
                    Accept: "application/json",
                },
                contentType: "application/json",
                dataType: "json",
                type: command.method,
                data: JSON.stringify(data),
                success: successPostCommand(success, data.loginName),
                error: errorPostCommand(error),
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

        function getCurrent(success) {
            $.ajax("/users/$current", {
                headers: {
                    Accept: "application/json",
                },
                dataType: "json",
                type: "GET",
                success: successGet(success),
                error: errorGet(),
            });
        }

        function getAll(success) {
            $.ajax("/users/", {
                headers: {
                    Accept: "application/json",
                },
                dataType: "json",
                type: "GET",
                success: successGet(success),
                error: errorGet(),
            });
        }

        function successPostCommand(success, login) {
            return function (data, status, xhr) {
                if (success)
                    success(login);
            };
        }

        function errorPostCommand(error) {
            return function (xhr, data, status) {
                if (error)
                    error(status);
                else 
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
            create: function (data, success, error) {
                postCommand({ method: "POST", endpoint: "" }, data, success, error);
            },
            update: function (data, success, error) {
                postCommand({ method: "PUT", endpoint: data.loginName }, data, success, error);
            },
            enable: function (data, success, error) {
                postCommand({ method: "POST", endpoint: data.loginName + "/command/enable" }, data, success, error);
            },
            disable: function (data, success, error) {
                postCommand({ method: "POST", endpoint: data.loginName + "/command/disable" }, data, success, error);
            },
            delete: function (data, success, error) {
                postCommand({ method: "DELETE", endpoint: data.loginName }, data, success, error);
            },
            setPassword: function (data, success, error) {
                postCommand({ method: "POST", endpoint: data.loginName + "/command/reset-password" }, data, success, error);
            },
            get: function (loginName, success) {
                get(loginName, success);
            },
            getCurrent: function (success) {
                getCurrent(success);
            },
            getAll: function (success) {
                getAll(success);
            }
        };
    }

    return {
        create: function () {
            return internalCreate();
        },
    };
});