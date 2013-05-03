"use strict";

var $projections = {
    createEventProcessor: function(_log, _notify) {
        var debugging = false;
        var eventHandlers = { };
        var anyEventHandlers = [];
        var rawEventHandlers = [];
        var transformers = [];
        var getStatePartitionHandler = function () {
            throw "GetStatePartition is not defined";
        };

        var sources = {
            /* TODO: comment out default falses to reduce message size */
            allStreams: false, 
            allEvents: false,
            byStreams: false,
            byCustomPartitions: false,
            categories: [], 
            streams: [], 
            events: [],
            definesStateTransform: false,
            options: { 
                resultStreamName: null, 
                partitionResultStreamNamePattern: null, 
                $forceProjectionName: null,
                $includeLinks: false,
                useEventIndexes: false,
                reorderEvents: false,
                processingLag: 0,
            }, 
        };

        var initStateHandler = function() { return { }; };

        var projectionState = null;

        var commandHandlers = {
            set_debugging: function () {
                debugging = true;
            },

            initialize: function() {
                projectionState = initStateHandler();
                return "OK";
            },

            get_state_partition: function (event, streamId, eventType, category, sequenceNumber, metadata) {
                return getStatePartition(event, streamId, eventType, category, sequenceNumber, metadata);
            },

            process_event: function (event, isJson, streamId, eventType, category, sequenceNumber, metadata, partition) {
                processEvent(event, isJson, streamId, eventType, category, sequenceNumber, metadata, partition);
                var stateJson = JSON.stringify(projectionState);
                return stateJson;
            },

            transform_state_to_result: function () {
                var result = projectionState;
                for (var i = 0; i < transformers.length; i++) {
                    var by = transformers[i];
                    result = by(result);
                    if (result === null)
                        break;
                }
                return result !== null ? JSON.stringify(result) : null;
            },

            set_state: function(jsonState) {
                var parsedState = JSON.parse(jsonState);
                projectionState = parsedState;
                return "OK";
            },

            get_sources: function() {
                return JSON.stringify(sources);
            }
        };

        function registerCommandHandlers($on) {
            // this is the only way to pass parameters to the system module

            for (var name in commandHandlers) {
                $on(name, commandHandlers[name]);
            }
        }

        function on_event(eventName, eventHandler) {
            eventHandlers[eventName] = eventHandler;
            sources.events.push(eventName);
        }

        function on_init_state(initHandler) {
            initStateHandler = initHandler;
        }

        function on_any(eventHandler) {
            sources.allEvents = true;
            anyEventHandlers.push(eventHandler);
        }

        function on_raw(eventHandler) {
            sources.allEvents = true;
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

         function tryDeserializeBody(eventEnvelope) {
             var eventRaw = eventEnvelope.bodyRaw;
            try {
                if (eventRaw == '')
                    eventEnvelope.body = {};
                else if (typeof eventRaw === "object")  //TODO: why do we need this?
                    eventEnvelope.body = eventRaw;
                else
                    eventEnvelope.body = JSON.parse(eventRaw);
            } catch (ex) {
                eventEnvelope.jsonError = ex;
                eventEnvelope.body = undefined;
            }
         }

        function envelope(body, bodyRaw, eventType, streamId, sequenceNumber, metadataRaw, partition) {
            this.body = body;
            this.bodyRaw = bodyRaw;;
            this.eventType = eventType;
            this.streamId = streamId;
            this.sequenceNumber = sequenceNumber;
            this.metadataRaw = metadataRaw;
            this.partition = partition;
            this.metadata_ = null;
        }

        Object.defineProperty(envelope.prototype, "metadata", {
            get: function () {
                if (!this.metadata_) {
                    this.metadata_ = JSON.parse(this.metadataRaw);
                }
                return this.metadata_;
            }
        });

        function getStatePartition(eventRaw, streamId, eventType, category, sequenceNumber, metadataRaw) {

             var eventHandler = getStatePartitionHandler;

             var eventEnvelope = new envelope(null, eventRaw, eventType, streamId, sequenceNumber, metadataRaw, null);

             tryDeserializeBody(eventEnvelope);

            var partition = eventHandler(eventEnvelope);

            var result;
            //TODO: warn/disable empty string
            if (partition === undefined || partition === null || partition === "")
                result = "";
            else
                result = partition.toString();
            return result;

        }

         function processEvent(eventRaw, isJson, streamId, eventType, category, sequenceNumber, metadataRaw, partition) {

            var eventName = eventType;

            var eventHandler;
            var state = projectionState;

            var index;

            var eventEnvelope = new envelope(null, eventRaw, eventType, streamId, sequenceNumber, metadataRaw, partition);
             // debug only
            for (index = 0; index < rawEventHandlers.length; index++) {
                eventHandler = rawEventHandlers[index];
                state = callHandler(eventHandler, state, eventEnvelope);
            }

            eventHandler = eventHandlers[eventName];

            if (isJson && (eventHandler !== undefined || anyEventHandlers.length > 0)) 
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
            sources.byStreams = true;
        }

        function partitionBy(eventHandler) {
            getStatePartitionHandler = eventHandler;
            sources.byCustomPartitions = true;
        }

        function $defines_state_transform() {
            sources.definesStateTransform = true;
        }

        function chainTransformBy(by) {
            transformers.push(by);
            sources.definesStateTransform = true;
        }

        function fromAll() {
            sources.allStreams = true;
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
            on_event: on_event,
            on_init_state: on_init_state,
            on_any: on_any,
            on_raw: on_raw,

            fromAll: fromAll,
            fromCategory: fromCategory,
            fromStream: fromStream,

            byStream: byStream,
            partitionBy: partitionBy,
            $defines_state_transform: $defines_state_transform,
            chainTransformBy: chainTransformBy,

            emit: emit,
            options: options,

            register_comand_handlers: registerCommandHandlers,
        };
    }
};
$projections;