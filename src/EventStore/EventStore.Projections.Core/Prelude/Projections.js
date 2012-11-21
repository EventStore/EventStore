"use strict";

var $projections = {
    createEventProcessor: function(_log, _notify) {
        var debugging = false;
        var eventHandlers = { };
        var anyEventHandlers = [];
        var rawEventHandlers = [];
        var sources = { 
            all_streams: false, 
            all_events: false, 
            categories: [], 
            streams: [], 
            events: [], 
            options: { 
                stateStreamName: null, 
                $forceProjectionName: null, 
                useEventIndexes: false,
            }, 
        };
        var initStateHandler = function() { return { }; };

        var projectionState = null;

        var commandHandlers = {
            set_debugging: function () {
                debugging = true;
            },
            initialize_raw: function() {
                projectionState = initStateHandler();
                return "OK";
            },

            process_event_raw: function (event, streamId, eventType, category, sequenceNumber, metadata, log_position) {
                processEvent(event, streamId, eventType, category, sequenceNumber, metadata, log_position);
                return "OK";
            },

            get_state_raw: function() {
                return projectionState;
            },

            set_state_raw: function(state) {
                projectionState = state;
                return "OK";
            },

            get_sources_raw: function() {
                return sources;
            }
        };


        function on_pure(eventName, eventHandler) {
            eventHandlers[eventName] = eventHandler;
            sources.events.push(eventName);
        }

        function on_init_state(initHandler) {
            initStateHandler = initHandler;
        }

        function on_any(eventHandler) {
            sources.all_events = true;
            anyEventHandlers.push(eventHandler);
        }

        function on_raw(eventHandler) {
            sources.all_events = true;
            rawEventHandlers.push(eventHandler);
        }

        function callHandler(handler, state, envelope) {
             if (debugging)
                 debugger;
             var newState = handler(state, envelope);
             if (newState === undefined)
                 newState = state;
             return newState;
         };

        function processEvent(eventRaw, streamId, eventType, category, sequenceNumber, metadataRaw, log_position) {

            var eventName = eventType;

            var eventHandler;
            var state = projectionState;

            var index;

            var eventEnvelope = {
                body: null,
                bodyRaw: eventRaw,
                eventType: eventType,
                streamId: streamId,
                sequenceNumber: sequenceNumber,
                metadataRaw: metadataRaw,
                logPosition: log_position,
            };
            // debug only
            for (index = 0; index < rawEventHandlers.length; index++) {
                eventHandler = rawEventHandlers[index];
                state = callHandler(eventHandler, state, eventEnvelope);
            }

            eventHandler = eventHandlers[eventName];

            if (eventHandler !== undefined || anyEventHandlers.length > 0) {
                try {
                    if (eventRaw == '')
                        eventEnvelope.body = {};
                    else if (typeof eventRaw === "object")
                        eventEnvelope.body = eventRaw;
                    else
                        eventEnvelope.body = JSON.parse(eventRaw);
                } catch (ex) {
                    eventEnvelope.jsonError = ex;
                    eventEnvelope.body = undefined;
                }
            }
            for (index = 0; index < anyEventHandlers.length; index++) {
                eventHandler = anyEventHandlers[index];
                state = callHandler(eventHandler, state, eventEnvelope);
            }

            eventHandler = eventHandlers[eventName];
            if (eventHandler !== undefined) {
                state = callHandler(eventHandler, state, eventEnvelope);
            }
            projectionState = state;
        }

        function fromStream(sourceStream) {
            sources.streams.push(sourceStream);
        }

        function fromCategory(sourceCategory) {
            sources.categories.push(sourceCategory);
        }

        function byStream() {
            sources.by_streams = true;
        }

        function fromAll() {
            sources.all_streams = true;
        }

        function emit(ev) {
            _notify("emit", JSON.stringify(ev));
        }

        function options(opts) {
            for (var name in opts) {
                if (sources.options[name] === undefined)
                    throw "Unrecognized option: " + name;
                sources.options[name] = opts[name];
            }
        }

        return {
            on_pure: on_pure,
            on_init_state: on_init_state,
            on_any: on_any,
            on_raw: on_raw,

            fromAll: fromAll,
            fromCategory: fromCategory,
            fromStream: fromStream,

            byStream: byStream,

            emit: emit,
            options: options,

            commandHandlers: commandHandlers,
        };
    }
};
$projections;