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

            get_state_partition: function (json, streamId, eventType, category, sequenceNumber, metadata) {
                return eventProcessor.commandHandlers.get_state_partition_raw(json, streamId, eventType, category, sequenceNumber, metadata);
            },
        
            process_event: function(json, streamId, eventType, category, sequenceNumber, metadata, partition) {
                return eventProcessor.commandHandlers.process_event_raw(json, streamId, eventType, category, sequenceNumber, metadata, partition);
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
            },
             
            set_debugging: function () {
                return eventProcessor.commandHandlers.set_debugging();
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

    function translateOn(handlers) {

        for (var name in handlers) {
            if (name == 0 || name === "$init") {
                eventProcessor.on_init_state(handlers[name]);
            }
            else if (name === "$any") {
                eventProcessor.on_any(handlers[name]);
            }
            else {
                eventProcessor.on_pure(name, handlers[name]);
            }
        }
    }


    function emitStateUpdated() {
        eventProcessor.emit_state_updated();
    }

    function when(handlers) {
        translateOn(handlers);
        return {
            emitStateUpdated: emitStateUpdated,
        };
    }

    function whenAny(handler) {
        eventProcessor.on_any(handler);
        return {
            emitStateUpdated: emitStateUpdated,
        };
    }

    function foreachStream() {
        eventProcessor.byStream();
        // NOTE: this may be removed in the future
        // currently we do not support foreach projections without emitStateUpdated
        eventProcessor.emit_state_updated();
        return {
            when: when,
            whenAny: whenAny,
        };
    }

    function partitionBy(byHandler) {
        eventProcessor.partitionBy(byHandler);
        // NOTE: this may be removed in the future
        // currently we do not support foreach projections without emitStateUpdated
        eventProcessor.emit_state_updated();
        return {
            when: when,
            whenAny: whenAny,
        };
    }

    function fromCategory(category) {
        eventProcessor.fromCategory(category);
        return {
            partitionBy: partitionBy,
            foreachStream: foreachStream,
            when: when,
            whenAny: whenAny,
        };
    }

    function fromAll() {
        eventProcessor.fromAll();
        return {
            partitionBy: partitionBy,
            when: when,
            whenAny: whenAny,
            foreachStream: foreachStream,
        };
    }

    function fromStream(stream) {
        eventProcessor.fromStream(stream);
        return {
            partitionBy: partitionBy,
            when: when,
            whenAny: whenAny,
        };
    }

    function fromStreams(streams) {
        for (var i = 0; i < streams.length; i++) 
            eventProcessor.fromStream(streams[i]);
        return {
            partitionBy: partitionBy,
            when: when,
            whenAny: whenAny,
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

    function options(options_obejct) {
        eventProcessor.options(options_obejct);
    }

    return {
        log: queryLog,

        on_any: eventProcessor.on_any,
        on_raw: eventProcessor.on_raw,

        fromAll: fromAll,
        fromCategory: fromCategory,
        fromStream: fromStream,
        fromStreams: fromStreams,
        options: options,
        emit: emit, 
        linkTo: linkTo, 
        require: modules.require,
    };
};

scope;
