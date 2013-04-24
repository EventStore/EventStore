if (!window.es) { window.es = {}; };
es.projection = function (settings) {

    var projectionBody = settings.body;
    var onStateUpdate = settings.onStateUpdate || function () { };
    var showError = settings.showError || function () { };
    var hideError = settings.hideError || function () { };

    var currentTimeout = null;
    var currentAjaxes = null;
    var category = null;

    return {
        start: startProjection,
        stop: stopProjection
    };

    function startProjection() {

        stopProjection();
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
            startPolling("$ce-" + category, processEvent);
        } else {
            category = null;
            startPolling(sources.streams[0], processEvent);
        }

        function processEvent(event) {
            var parsedEvent = event;

            var stateStr = processor.process_event(parsedEvent.data,
                            "1", // isJson
                            parsedEvent.eventStreamId,
                            parsedEvent.eventType,
                            category,
                            parsedEvent.eventNumber,
                            parsedEvent.metadata);
            var stateObj = JSON.parse(stateStr);

            onStateUpdate(stateObj, stateStr);
        }
    };

    function stopProjection() {
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
    
    function startPolling(streamId, callback) {
        var nextPageUrl = '/streams/' + encodeURIComponent(streamId);
        var readNextPage = readFirstPage;

        readFirstPage();

        function readFirstPage() {
            $.ajax(
                nextPageUrl,
                {
                    headers: {
                        Accept: 'application/json'
                    },
                    success: function(page) {
                        if (!page.entries || page.entries.length === 0) {
                            setTimeout(readNextPage, 1000);
                            return;
                        }
                        nextPageUrl = $.grep(page.links, function(link) { return link.relation === 'last'; })[0].uri;
                        readNextPage = readForwardPage;
                        setTimeout(readNextPage, 0);
                    },
                    error: function(jqXhr, status, error) {
                        setTimeout(readNextPage, 1000);
                    }
                });
        }

        function readForwardPage() {
            $.ajax(
                nextPageUrl + "?embed=content",
                {
                    headers: {
                        Accept: 'application/json'
                    },
                    success: function (page) {
                        if (!page.entries || page.entries.length === 0) {
                            setTimeout(readNextPage, 1000);
                            return;
                        }

                        var nextPage = $.grep(page.links, function(link) { return link.relation === 'previous'; })[0].uri;
                        for (var i = 0, n = page.entries.length; i < n; i += 1) {
                            callback(page.entries[i].content);
                        }
                        nextPageUrl = nextPage;
                        setTimeout(readNextPage, 0);
                    },
                    error: function() {
                        setTimeout(readNextPage, 1000);
                    }
                });
        }
    }
};