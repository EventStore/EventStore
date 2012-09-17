"use strict";
// these $ globals are defined by external environment
// they are redefined here to make R# like tools understand them
var _log = $log;
var _load_module = $load_module;

function log(message) {
    _log("P: " + message);
}

function initializeModules() {
    // load module load new instance of the given module every time
    // this is a responsibility of prelude to manage instances of modules
    var modules = _load_module('Modules');

    // TODO: replace with createRequire($load_module)
    modules.$load_module = _load_module;

    return modules;
} 

function initializeProjections() {
    var projections = _load_module('Projections');
    return projections;
}

var modules = initializeModules();
var projections = initializeProjections();

function scope($on, $notify) {
    var eventProcessor = projections.createEventProcessor(log, $notify);
    var commandHandlers = {
            initialize: function() {
                return eventProcessor.commandHandlers.initialize_raw();
            }, 
        
            process_event: function(json, streamId, eventType, category, sequenceNumber, metadata) {
                return eventProcessor.commandHandlers.process_event_raw(json, streamId, eventType, category, sequenceNumber, metadata);
            }, 

            test_array: function(json, a, b, c) {
                return (a + "__" + b + "__" + c).toString();
            }, 

            get_state: function() {
                var stateJson = JSON.stringify(eventProcessor.commandHandlers.get_state_raw());
                return stateJson;
            }, 
        
            set_state: function(json) {
                var projectionState = JSON.parse(json);
                return eventProcessor.commandHandlers.set_state_raw(projectionState);
            }, 
        
            get_statistics: function() {
                return JSON.stringify(eventProcessor.commandHandlers.get_statistics_raw());
            }, 
        
            get_sources: function() {
                return JSON.stringify(eventProcessor.commandHandlers.get_sources_raw());
            }
    };

    // this is the only way to pass parameters to the system module

    function registerCommandHandlers($on) {
        for (var name in commandHandlers) {
            $on(name, commandHandlers[name]);
        }
    }

    registerCommandHandlers($on);


    function queryLog(message) {
        _log(message);
    }

    function on(eventName, eventHandler) {
        $log("Obsoleted: 'on'");
        eventProcessor.on_pure(eventName, function (state, ev) {
            eventHandler.call(state, ev);
            return state;
        });
    }

    function on_pure(eventName, eventHandler) {
        $log("Obsoleted: 'on_pure'");
        eventProcessor.on_pure(eventName, function (state, ev) {
            state = eventHandler(state, ev);
            return state;
        });
    }

    function translateOn(handlers) {

        for (var name in handlers) {
            if (name == 0) {
                eventProcessor.on_init_state(handlers[0]);
            }
            else {
                eventProcessor.on_pure(name, handlers[name]);
            }
        }
    }

    function fromCategory(category) {
        eventProcessor.fromCategory(category);
        return {
            foreachStream: function () {
                eventProcessor.byStream();
                return {
                    when: function (handlers) {
                        translateOn(handlers);
                    },
                    whenAny: function (handler) {
                        eventProcessor.on_any(handler);
                    }
                };
            },
            when: function (handlers) {
                translateOn(handlers);
            },
            whenAny: function (handler) {
                eventProcessor.on_any(handler);
            }
        };
    }

    function fromAll() {
        eventProcessor.fromAll();
        return {
            foreachStream: function () {
                eventProcessor.byStream();
                return {
                    when: function (handlers) {
                        translateOn(handlers);
                    },
                    whenAny: function (handler) {
                        eventProcessor.on_any(handler);
                    }
                };
            },
            when: function (handlers) {
                translateOn(handlers);
            },
            whenAny: function (handler) {
                eventProcessor.on_any(handler);
            }
        };
    }

    function fromStream(stream) {
        eventProcessor.fromStream(stream);
        return {
            when: function (handlers) {
                translateOn(handlers);
            },
            whenAny: function (handler) {
                eventProcessor.on_any(handler);
            }
        };
    }

    function fromStreams(streams) {
        for (var i = 0; i < streams.length; i++) 
            eventProcessor.fromStream(streams[i]);
        return {
            when: function (handlers) {
                translateOn(handlers);
            },
            whenAny: function (handler) {
                eventProcessor.on_any(handler);
            }
        };
    }

    function emit(streamId, eventName, eventBody) {
        var message = { streamId: streamId, eventName: eventName , body: JSON.stringify(eventBody) };
        eventProcessor.emit(message);
    }

    function linkTo(streamId, event) {
        var message = { streamId: streamId, eventName: "$>", body: event.sequenceNumber + "@" + event.streamId };
        eventProcessor.emit(message);
    }



    return {
        log: queryLog,
        on: on,

        on_pure: on_pure,
        on_any: eventProcessor.on_any,
        on_raw: eventProcessor.on_raw,

        fromAll: fromAll,
        fromCategory: fromCategory,
        fromStream: fromStream,
        fromStreams: fromStreams,
        emit: emit, 
        linkTo: linkTo, 
        require: modules.require,
    };
};

scope;
