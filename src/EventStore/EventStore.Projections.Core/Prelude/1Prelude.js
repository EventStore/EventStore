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
    eventProcessor.register_comand_handlers($on);
    
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
                eventProcessor.on_event(name, handlers[name]);
            }
        }
    }


    function $defines_state_transform() {
        eventProcessor.$defines_state_transform();
    }

    function transformBy(by) {
        eventProcessor.chainTransformBy(by);
        return {
            transformBy: transformBy,
            filterBy: filterBy,
            outputTo: outputTo,
        };
    }

    function filterBy(by) {
        eventProcessor.chainTransformBy(function (s) {
            var result = by(s);
            return result ? s : null;
        });
        return {
            transformBy: transformBy,
            filterBy: filterBy,
            outputTo: outputTo,
        };
    }

    function outputTo(resultStream, partitionResultStreamPattern) {
        eventProcessor.$defines_state_transform();
        eventProcessor.options({
            resultStreamName: resultStream,
            partitionResultStreamNamePattern: partitionResultStreamPattern,
        });
    }

    function when(handlers) {
        translateOn(handlers);
        return {
            $defines_state_transform: $defines_state_transform,
            transformBy: transformBy,
            filterBy: filterBy,
            outputTo: outputTo,
        };
    }

    function whenAny(handler) {
        eventProcessor.on_any(handler);
        return {
            $defines_state_transform: $defines_state_transform,
            transformBy: transformBy,
            filterBy: filterBy,
            outputTo: outputTo,
        };
    }

    function foreachStream() {
        eventProcessor.byStream();
        return {
            when: when,
            whenAny: whenAny,
        };
    }

    function partitionBy(byHandler) {
        eventProcessor.partitionBy(byHandler);
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
        var arr = Array.isArray(streams) ? streams : arguments;
        for (var i = 0; i < arr.length; i++) 
            eventProcessor.fromStream(arr[i]);
 
        return {
            partitionBy: partitionBy,
            when: when,
            whenAny: whenAny,
        };
    }

    function emit(streamId, eventName, eventBody, metadata) {
        var message = { streamId: streamId, eventName: eventName , body: JSON.stringify(eventBody), metadata: metadata };
        eventProcessor.emit(message);
    }

    function linkTo(streamId, event, metadata) {
        var message = { streamId: streamId, eventName: "$>", body: event.sequenceNumber + "@" + event.streamId, metadata: metadata };
        eventProcessor.emit(message);
    }

    function copyTo(streamId, event, metadata) {
        var m = {};

        var em = event.metadata;
        if (em)
            for (var p1 in em)
                if (p1.indexOf("$") !== 0)
                    m[p1] = em[p1];

        if (metadata) 
            for (var p2 in metadata)
                if (p2.indexOf("$") !== 0)
                    m[p2] = metadata[p2];

        var message = { streamId: streamId, eventName: event.eventType, body: event.bodyRaw, metadata: m };
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
        copyTo: copyTo,
        require: modules.require,
    };
};

scope;
