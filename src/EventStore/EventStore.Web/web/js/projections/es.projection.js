if (!window.es) { window.es = {}; };
es.projection = function (settings) {

    var projectionBody = settings.body;
    var onStateChange = settings.onStateChange;
    var runTrigger = settings.runTrigger || $; // trigger to run projections, default is onload
    var startFrom = settings.startFrom || 0;
    var showError = settings.showError || function () { };
    var hideError = settings.hideError || function () { };

    var currentTimeout = null;
    var currentAjaxes = null;
    var category = null;

    runTrigger(function () {

        cancelProjection();
        var processor = $initialize_hosted_projections();
        projectionBody();
        processor.initialize();

        var sources = JSON.parse(processor.get_sources());
        if (sources.all_streams
            || (sources.categories != null && sources.categories.length > 1)
            || (sources.streams != null && sources.streams.length > 1)) {
            throw "Unsupported projection source to run in the web browser";
        }

        if (sources.categories != null && sources.categories.length == 1) {
            category = sources.categories[0];
            startPolling("$ce-" + category, startFrom, process_event);
        } else {
            category = null;
            startPolling(sources.streams[0], startFrom, process_event);
        }

        function process_event(event) {
            var parsedEvent = event;

            processor.process_event(parsedEvent.data,
                            parsedEvent.eventStreamId,
                            parsedEvent.eventType,
                            category,
                            parsedEvent.eventNumber,
                            parsedEvent.metadata);
            var state = processor.get_state();

            onStateChange(state);
        }
    });

    function cancelProjection() {
        if (currentTimeout !== null)
            clearTimeout(currentTimeout);
        if (currentAjaxes !== null) {
            for (var i = 0, l = currentAjaxes.length; i < l; i++) {
                currentAjaxes[i].abort();
            }
        }

        currentAjaxes = null;
        currentTimeout = null;
    };

    function startPolling(streamId, startingSequenceNumber, callback) {

        currentAjaxes = [];

        var usualBatchSize = 15;
        loadNextBatch(startingSequenceNumber, usualBatchSize);

        function loadNextBatch(sequenceNumber, batchSize) {
            var processedBatchItemsCount = 0;
            var receivedEvents = [];

            var encodedStream = encodeURIComponent(streamId);

            for (var i = 0; i < batchSize; i++) {
                var url = ["/streams/", encodedStream, "/event/", sequenceNumber + i].join("");
                var ajax = $.ajax(url, {
                    headers: {
                        "Accept": "application/json"
                    },
                    contentType: "application/json",
                    cache: false,
                    success: successFeed,
                    error: errorFeed
                });
                currentAjaxes.push(ajax);
            }

            function successFeed(data) {
                hideError();
                receivedEvents.push(data);
                processBatchItem();
            }

            function errorFeed(jqXHR, status, error) {
                if (jqXHR.status == 404) {
                    showError("Reached the end of the stream or stream was not found");
                } else {
                    showError("Error occured: " + error);
                }

                processBatchItem();
            }

            function processBatchItem() {
                processedBatchItemsCount++;

                if (processedBatchItemsCount === batchSize) {
                    currentAjaxes = [];   // no easy way to remove ajaxes from array when they arrive, so just remove all when batch done

                    var successfullReads = receivedEvents.length;
                    if (successfullReads < batchSize) {
                        loadNextBatchInAWhile(sequenceNumber + successfullReads, 1);
                    } else {
                        loadNextBatch(sequenceNumber + batchSize, usualBatchSize);
                    }

                    processBatch(receivedEvents);
                    receivedEvents = null;
                }

                function loadNextBatchInAWhile(sequenceNumber, batchSize) {
                    currentTimeout = setTimeout(function () {
                        loadNextBatch(sequenceNumber, batchSize);
                    }, 1000);
                }
            }

            function processBatch(events) {
                events.sort(function (a, b) {
                    return a.eventNumber - b.eventNumber;
                });

                for (var j = 0, l = events.length; j < l; j++) {
                    callback(events[j]);
                }
            }
        }
    }
};
