"use strict";

var $projections = {
    createEventProcessor: function(_log, _notify) {

        var eventHandlers = { };
        var anyEventHandlers = [];
        var rawEventHandlers = [];
        var getStatePartitionHandler = function () {
            throw "GetStatePartition is not defined";
        };

        var sources = {
            /* TODO: comment out default falses to reduce message size */
            all_streams: false, 
            all_events: false,
            by_streams: false,
            by_custom_partitions: false,
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
            initialize_raw: function() {
                projectionState = initStateHandler();
                return "OK";
            },

            get_state_partition_raw: function (event, streamId, eventType, category, sequenceNumber, metadata, log_position) {
                return getStatePartition(event, streamId, eventType, category, sequenceNumber, metadata, log_position);
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
             var newState = handler(state, envelope);
             if (newState === undefined)
                 newState = state;
             return newState;
         };

         function tryDeserializeBody(eventEnvelope) {
             var eventRaw = eventEnvelope.bodyRaw;
            try {
                if (eventRaw == '')
                    eventEnvelope.body = {};
                else
                    eventEnvelope.body = JSON.parse(eventRaw);
            } catch (ex) {
                eventEnvelope.jsonError = ex;
                eventEnvelope.body = undefined;
            }
        }

        function getStatePartition(eventRaw, streamId, eventType, category, sequenceNumber, metadataRaw) {

             var eventHandler = getStatePartitionHandler;

             var eventEnvelope = {
                 body: null,
                 bodyRaw: eventRaw,
                 eventType: eventType,
                 streamId: streamId,
                 sequenceNumber: sequenceNumber,
                 metadataRaw: metadataRaw,
             };

             tryDeserializeBody(eventEnvelope);

             return eventHandler(eventEnvelope);

         }

         function processEvent(eventRaw, streamId, eventType, category, sequenceNumber, metadataRaw, partition) {

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
                partition: partition,
            };
            // debug only
            for (index = 0; index < rawEventHandlers.length; index++) {
                eventHandler = rawEventHandlers[index];
                state = callHandler(eventHandler, state, eventEnvelope);
            }

            eventHandler = eventHandlers[eventName];

            if (eventHandler !== undefined || anyEventHandlers.length > 0) 
                tryDeserializeBody(eventEnvelope);

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

        function partitionBy(eventHandler) {
            getStatePartitionHandler = eventHandler;
            sources.by_custom_partitions = true;
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
            partitionBy: partitionBy,

            emit: emit,
            options: options,

            commandHandlers: commandHandlers,
        };
    }
};
$projections;