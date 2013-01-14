"use strict";
// url monitor
define([], function () {
    return function urlMonitor(baseUrl, accept, type) {

        var _lastDataJson = null;
        var _handler = null;
        var _current = null;

        function success(data, status, xhr) {
            var dataJson = JSON.stringify(data);
            if (dataJson !== _lastDataJson) {
                _lastDataJson = dataJson;
                _handler(data);
            }
            setTimeout(requestData, 1000);
        }

        function error(xhr, status) {
            setTimeout(requestData, 1000);
        }

        function requestData() {
            if (_handler !== null) {
                _current = $.ajax(baseUrl, {
                    headers: {
                        Accept: accept,
                    },
                    dataType: type,
                    success: success,
                    error: error,
                });
            }
        }

        return {
            start: function (handler) {
                _handler = handler;
                requestData();
            },

            stop: function () {
                _handler = null;
                _current.abort();
            }
        };

    };
});