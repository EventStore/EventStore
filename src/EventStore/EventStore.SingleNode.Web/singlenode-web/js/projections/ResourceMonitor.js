"use strict";
// url monitor
define([], function () {
    return { 
        create: function createResourceMonitor(baseUrl, options) {

            var _lastDataJson = null;
            var _handler = null;
            var _current = null;
            var _timeout = null;

            function schedule() {
                _timeout = setTimeout(requestData, 1000);
            }

            function success(data, status, xhr) {
                _current = null;
                var dataJson = JSON.stringify(data);
                if (dataJson !== _lastDataJson) {
                    _lastDataJson = dataJson;
                    _handler(data, xhr);
                }
                if (options.autoRefresh)
                    schedule();
            }

            function error(xhr, status) {
                _current = null;
                schedule();
            }

            function requestData() {
                if (_handler !== null) {
                    _current = $.ajax(baseUrl, {
                        headers: {
                            Accept: options.accept,
                        },
                        dataType: options.type,
                        success: success,
                        error: error,
                    });
                }
            }

            return {
                start: function(handler) {
                    _handler = handler;
                    requestData();
                },

                stop: function() {
                    _handler = null;
                    _current.abort();
                    _current = null;
                },

                poll: function () {
                    if (_current !== null) {
                        _current.abort();
                        _current = null;
                    }
                    if (_timeout !== null) {
                        clearTimeout(_timeout);
                    }
                    _timeout = setTimeout(requestData, 0);
                }

            };
        }
    };
});