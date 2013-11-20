"use strict";
// url monitor
define([], function () {
    return { 
        create: function createResourceMonitor(baseUrl, options) {

            var _lastDataJson = null;
            var _successHandler = null;
            var _transientErrorHandler = null;
            var _goneHandler = null;
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
                    _successHandler(data, xhr);
                }
                if (options.autoRefresh)
                    schedule();
            }

            function error(xhr, status) {
                _current = null;
                switch (xhr.status) {
                    case 404:
                        if (_goneHandler)
                            _goneHandler();
                        stop();
                        return;
                    default:
                        if (_transientErrorHandler)
                            _transientErrorHandler();
                        break;
                }
                schedule();
            }

            function requestData() {
                if (_successHandler !== null) {
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

            function stop() {
                _successHandler = null;
                _transientErrorHandler = null;
                _goneHandler = null;
                if (_current)
                    _current.abort();
                _current = null;
            }

            return {
                start: function(successHandler, transientErrorHandler, goneHandler) {
                    _successHandler = successHandler;
                    _transientErrorHandler = transientErrorHandler;
                    _goneHandler = goneHandler;
                    requestData();
                },

                stop: stop,

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