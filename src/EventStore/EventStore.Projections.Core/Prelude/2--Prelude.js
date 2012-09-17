"use strict";
// these $ globals are defined by external environment
// they are redefined here to make R# like tools understand them
var _log = $log;
var _load_module = $load_module;
var eventId = new Date().valueOf();

function initializeModules() {
    // load module load new instance of the givem module every time
    // this is a responsibility of prelude to manage instancs of modules
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

// above is the standard prelude initialization


function scope($on, $notify) {

    function queryLog(message) {
        _log("Q: " + message);
    }

    function translateOn(handlers, eventProcessor) {

        for (var name in handlers) {
            if (name == 0) {
                eventProcessor.on_init_state(handlers[0]);                
            }
            else {
                eventProcessor.on_pure(name, handlers[name]);
            }
        }
    }

    function registerCommandHandlers($on) {
        for (var name in commandHandlers) {
            $on(name, commandHandlers[name]);
        }
    }

    function createProjection(eventProcessor) {
        return {
            initialize_raw: function () {
                return eventProcessor.commandHandlers.initialize_raw();
            },

            process_event_raw: function(event) {
                return eventProcessor.commandHandlers.process_event_raw(event);
            }, 

            get_state_raw: function () {
                return eventProcessor.commandHandlers.get_state_raw();
            },

            set_state_raw: function (state) {
                return eventProcessor.commandHandlers.set_state_raw(state);
            },

            get_statistics_raw: function () {
                return eventProcessor.commandHandlers.get_statistics_raw();
            },

            get_sources_raw: function () {
                return eventProcessor.commandHandlers.get_sources_raw();
            }

        };
    }

    var startedAt = null;
    var count = 0;
    var projectionState = null;


    var definedProjections = [];
    var streamCategories = {};

    function process_event_raw(event) {

        var eventName = event.event.header.name;
        var eventBody = event.event.body;

        if (eventName === "StreamCreated") {
            streamCategories[eventBody.streamName] = eventBody.category;
        }

        for (var i = 0; i < definedProjections.length; i++) {
            definedProjections[i].process_event_raw(event);
        }
        count++;
        return "OK";
    };

    var commandHandlers = {
            initialize: function() {
                startedAt = new Date();
                count = 0;
                projectionState = { };

                for (var i = 0; i < definedProjections.length; i++) {
                    definedProjections[i].initialize_raw();
                }

                return "OK";
            }, 
        
            process_event: function(json) {
                var event = JSON.parse(json);

                return process_event_raw(event);
            }, 

            //TODO: this way of state partition handling is incompatible with persistence
        
            get_state: function() {
                var result = [];
                for (var i = 0; i < definedProjections.length; i++) {
                    result[i] = definedProjections[i].get_state_raw(); 
                }
                return JSON.stringify(result);
            }, 
        
            set_state: function(json) {
                var states = JSON.parse(json);
                for (var i = 0; i < definedProjections.length; i++) {
                    definedProjections[i].set_state_raw(states[i]); //TODO: avoid JSON
                }
                return "OK";
            }, 
        
            get_statistics: function() {
                var finishedAt = new Date();
                var totalTime = finishedAt.valueOf() - startedAt.valueOf();
                return JSON.stringify({
                    count: count,
                    stated_at: startedAt,
                    finished_at: finishedAt,
                    total_time: totalTime,
                    events_per_second: (count * 1000.0 / totalTime),
                });
            }, 
        
            get_sources: function() {
                var result = { all_streams: false, all_events: false, categories: [], streams: [], events: [] };
                for (var i = 0; i < definedProjections.length; i++) {
                    var sources = definedProjections[i].get_sources_raw(); 
                    result.all_streams = result.all_streams || sources.all_streams;
                    result.all_events = result.all_events || sources.all_events;

                    result.categories = result.categories.concat(sources.categories); // TODO: distinct?
                    result.streams = result.streams.concat(sources.streams); // TODO: distinct?
                    result.events = result.events.concat(sources.events); // TODO: distinct?
                }
                
                return JSON.stringify(result);
            }
    };


    function fromCategory(category) {
        var eventProcessor = projections.createEventProcessor(_log, $notify);
        definedProjections.push(createProjection(eventProcessor));
        return {
            foreachStream: function () {
                return {
                    on: function (handlers) {
                        eventProcessor.foreachInCategory(category);
                        translateOn(handlers, eventProcessor);
                    }
                };
            },
            on: function (handlers) {
                eventProcessor.fromCategory(category);
                translateOn(handlers, eventProcessor);
            }
        };
    }

    function fromAll() {
        var eventProcessor = projections.createEventProcessor(_log, $notify);
        definedProjections.push(createProjection(eventProcessor));
        return {
            on: function (handlers) {
                eventProcessor.fromAll();
                translateOn(handlers, eventProcessor);
            }
        };
    }

    function emit(category, aggregateId, eventName, eventBody) {
        var streamName = "/" + category + "/" + aggregateId;
        if (streamCategories[streamName] === undefined) {
            var streamCreated = { stream: "/" + category, event: { header: { id: eventId++, name: "StreamCreated" }, body: { category: category, streamName: streamName}} };
            var streamCreatedJson =  JSON.stringify(streamCreated);
            commandHandlers.process_event(streamCreatedJson);
        }
        var message = { stream: streamName, event: { header: { id: eventId++, name: eventName }, body: eventBody} };
        var messageJson =  JSON.stringify(message);
        //TODO: this should not be inside script;  pass it outside; Js process_handle should be called by C#
        commandHandlers.process_event(messageJson);
    }

    registerCommandHandlers($on);

    return {
        log: queryLog,
        fromCategory: fromCategory,
        fromAll: fromAll,
        emit: emit,
        require: modules.require,
    };
};

scope;



/*
var knownAggregates = new Array();
var counter = 0;
var eventId = new Date().valueOf();

on_pure("TransactionOccurred", function (state, ev) {
    counter++;
    var accountNo = Math.floor(Math.random() * (counter + 30) / 20);
    var streamName = "/Accounts/" + accountNo;
    if (!knownAggregates.hasOwnProperty(accountNo)) {
        emit({ stream: "/Accounts", event: { header: { id: eventId++, name: "StreamCreated" }, body: { streamName: streamName}} });
        knownAggregates[accountNo] = true;
        emit({ stream: streamName, event: { header: { id: eventId++, name: "AccountCreated" }, body: { accountNo: accountNo}} });
    }
    emit({ stream: streamName, event: { header: { id: eventId++, name: "TransactionOccurred" }, body: { total: ev.total}} });
    return {};
});
*/