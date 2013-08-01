if (!window.es) { window.es = {}; };
es.projection = function (settings) {

    var projectionBody = settings.body;
    var onStateUpdate = settings.onStateUpdate || function () { };
    var showError = settings.showError || function () { };
    var hideError = settings.hideError || function () { };

    var currentTimeout = null;
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
            var stateStr = processor.process_event(event.data,
                                                   "1", // isJson
                                                   event.eventStreamId,
                                                   event.eventType,
                                                   category,
                                                   event.eventNumber,
                                                   event.metadata);
            var stateObj = JSON.parse(stateStr);
            onStateUpdate(stateObj, stateStr);
        }
    }

    function stopProjection() {
        if (currentTimeout !== null)
            clearTimeout(currentTimeout);
        currentTimeout = null;
    };
    
    function getFeedLink(links, linkRel) {
        var res = $.grep(links, function (link) { return link.relation === linkRel; });
        return res.length ? res[0].uri : null;
    }

    function startPolling(streamId, callback) {
        var nextPageUrl = '/streams/' + encodeURIComponent(streamId);
        var readNextPage = readFirstPage;

        readFirstPage();

        function readFirstPage() {
            currentTimeout = null;
            $.ajax(
                nextPageUrl + "?embed=content",
                {
                    headers: {
                        Accept: 'application/json'
                    },
                    success: function(page) {
                        var lastLink = getFeedLink(page.links, 'last');
                        if (!lastLink) {
                            // head is the last page already
                            for (var i = 0, n = page.entries.length; i < n; i += 1) {
                                callback(page.entries[n - i - 1].content);
                            }
                            nextPageUrl = getFeedLink(page.links, 'previous');
                        } else {
                            nextPageUrl = lastLink;
                        }
                        readNextPage = readForwardPage;
                        currentTimeout = setTimeout(readNextPage, 0);
                    },
                    error: function(jqXhr, status, error) {
                        currentTimeout = setTimeout(readNextPage, 1000);
                    }
                });
        }

        function readForwardPage() {
            currentTimeout = null;
            $.ajax(
                nextPageUrl + "?embed=content",
                {
                    headers: {
                        Accept: 'application/vnd.eventstore.atom+json'
                    },
                    success: function (page) {
                        for (var i = 0, n = page.entries.length; i < n; i += 1) {
                            callback(page.entries[n - i - 1].content);
                        }
                        var prevLink = getFeedLink(page.links, 'previous');
                        nextPageUrl = prevLink || nextPageUrl;
                        currentTimeout = setTimeout(readNextPage, prevLink ? 0 : 1000);
                    },
                    error: function() {
                        currentTimeout = setTimeout(readNextPage, 1000);
                    }
                });
        }
    }
};
