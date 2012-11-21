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
            startPolling("$ce-" + category, process_event);
        } else {
            category = null;
            startPolling(sources.streams[0], process_event);
        }

        function process_event(event) {
            var parsedEvent = event;

            processor.process_event(parsedEvent.data,
                            parsedEvent.eventStreamId,
                            parsedEvent.eventType,
                            category,
                            parsedEvent.eventNumber,
                            parsedEvent.metadata);
            var stateStr = processor.get_state();
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

        var lastPageUrl = '/streams/' + encodeURIComponent(streamId);
        var lastProcessedPageUrl = null;
        var lastProcessedEntry = null;

        // not used yet - when something fails we just retry
        var defaultFail = function(a, b, c) { alert('Failed!'); };

        readAll(null, null);

        function readAll(fromPageUrl, fromEntry) {

            lastProcessedPageUrl = fromPageUrl;
            lastProcessedEntry = fromEntry;

            readLastPage({
                pageRead: pageRead,
                noEntries: noEntries,
                fail: defaultFail
            });

            function pageRead(firstPageUrl, lastEntry) {

                // check for end of stream
                if (lastProcessedEntry !== null && Entry.isNewerOrSame(lastProcessedEntry, lastEntry)) {
                    delayedReadAll(lastProcessedPageUrl, lastProcessedEntry);
                    return;
                }

                readRange({
                    page: fromPageUrl || firstPageUrl,
                    from: fromEntry || null,
                    to: lastEntry,
                    processEvent: callback,
                    endOfStream: delayedReadAll,
                    success: function (lastReadPageUrl, lastReadEntry) { readAll(lastReadPageUrl, lastReadEntry); },
                    fail: defaultFail
                });
            }

            function noEntries() {
                delayedReadAll(lastProcessedPageUrl, lastProcessedEntry);
            }

            function delayedReadAll(page, entry) {
                setTimeout(function () { readAll(page, entry); }, 1000);
            }
        }

        function readLastPage(sets) {

            var pageRead = sets.pageRead;
            var noEntries = sets.noEntries;
            var fail = sets.fail;

            $.ajax(lastPageUrl, {
                headers: {
                    'Accept': 'application/json'
                },
                success: function (page) {
                    if (page.entries.length === 0) {
                        noEntries();
                    }
                    var lastEntry = page.entries[0];
                    var firstPage = $.grep(page.links, function (link) { return link.relation === 'first'; })[0].uri;
                    pageRead(firstPage, lastEntry);
                },
                error: function (jqXhr, status, error) {
                    setTimeout(function () { readLastPage(sets); }, 1000);
                    //fail.apply(window, arguments);
                }
            });
        }

        function readRange(sets) {

            var page = sets.page;
            var from = sets.from;
            var to = sets.to;
            var processEvent = sets.processEvent;
            var success = sets.success;
            var fail = sets.fail;

            readByPages(page);

            function readByPages(fromPage) {
                readPage({
                    url: fromPage,
                    lowerBound: from,
                    upperBound: to,
                    processEvent: processEvent,
                    onPageRead: function (nextPage) {
                        readByPages(nextPage);
                    },
                    onUpperBound: function (lastReadPageUrl, lastReadEntry) {
                        success(lastReadPageUrl, lastReadEntry);
                    },
                    fail: fail
                });
            }
        }

        function readPage(sets) {

            var pageUrl = sets.url;
            var fromEntry = sets.lowerBound;
            var toEntry = sets.upperBound;
            var processEvent = sets.processEvent;
            var onPageRead = sets.onPageRead;
            var onUpperBound = sets.onUpperBound;
            var fail = sets.fail;

            $.ajax(pageUrl, {
                headers: {
                    'Accept': 'application/json'
                },
                success: function (page) {
                    var nextPage = $.grep(page.links, function (link) { return link.relation === 'prev'; })[0].uri;
                    var entries = $.grep(page.entries, function (entry) {
                        // if we've read more entries then we were asked to - it's ok - just set lastEntry correctly
                        return fromEntry === null || Entry.isNewer(entry, fromEntry);
                    });
                    var onEntriesRead = null;

                    if (Entry.isOnPage(pageUrl, toEntry)) {

                        // setting LastEntry as null is ok - readAll will just continue reading from beginning of page. And as deleted events won't appear again - no duplicates will be processed

                        if (entries.length === 0) {
                            onUpperBound(pageUrl, toEntry);
                            return;
                        }

                        var lastEntry = Entry.max(entries);
                        onEntriesRead = function () { onUpperBound(pageUrl, lastEntry); };
                    } else {
                        onEntriesRead = function () { onPageRead(nextPage); };
                    }

                    if (entries.length === 0) {
                        onPageRead(nextPage); // probably was deleted by maxAge/maxCount
                        return;
                    }

                    getEvents(entries, processEvent, onEntriesRead);
                },
                error: function () {
                    setTimeout(function () { readPage(sets); }, 1000);
                }
            });



            function getEvents(entries, processEvent, onFinish) {

                var eventsUrls = $.map(entries, function (entry) {
                    var jsonLink = $.grep(entry.links, function (link) { return link.type === 'application/json'; })[0].uri;
                    return jsonLink;
                });

                var eventsUrlsCount = eventsUrls.length;
                var processedEventUrlsCount = 0;
                var receivedEvents = [];

                currentAjaxes = [];

                for (var i = 0; i < eventsUrlsCount; i++) {
                    var url = eventsUrls[i];
                    var ajax = $.ajax(url, {
                        headers: {
                            "Accept": "application/json"
                        },
                        dataType: 'json',
                        success: successFeed,
                        error: errorFeed
                    });
                    currentAjaxes.push(ajax);
                }

                function successFeed(data) {
                    receivedEvents.push(data);
                    processBatchItem();
                }

                function errorFeed(jqXHR, status, error) {
                    if (jqXHR.responseCode === 404) {
                        // do nothing. entry may have been erased by maxAge/maxCount
                        processBatchItem();
                    } else {
                        // throw 'TODO: consider what to do if server is down or busy'
                    }
                }

                function processBatchItem() {
                    processedEventUrlsCount++;

                    if (processedEventUrlsCount === eventsUrlsCount) {
                        currentAjaxes = []; // no easy way to remove ajaxes from array when they arrive, so just remove all when batch done

                        var successfullReads = receivedEvents.length;
                        // can't do much about unsuccessfull reads :\

                        processReceivedEvents(receivedEvents);
                        receivedEvents = null;

                        onFinish();
                    }
                }

                function processReceivedEvents(events) {
                    events.sort(function (a, b) {
                        return a.eventNumber - b.eventNumber;
                    });

                    for (var j = 0, l = events.length; j < l; j++) {
                        processEvent(events[j]);
                    }
                }
            }
        }

        var Entry = {};
        Entry.isNewer = function (entry1, entry2) {
            return Entry.compare(entry1, entry2) > 0;
        };
        Entry.isNewerOrSame = function (entry1, entry2) {
            return Entry.compare(entry1, entry2) >= 0;
        };
        Entry.isOlderOrSame = function (entry1, entry2) {
            return Entry.compare(entry1, entry2) <= 0;
        };
        Entry.compare = function (entry1, entry2) {
            return Entry.getId(entry1) - Entry.getId(entry2);
        };
        Entry.getId = function (entry) {
            var strId = entry.id.substring(entry.id.lastIndexOf("/") + 1, entry.id.length);
            return parseInt(strId);
        };
        Entry.isOnPage = function (pageUrl, entry) {
            var entryId = Entry.getId(entry);

            // example: http://127.0.0.1:2114/streams/$stats-127.0.0.1:2114/range/39/20
            var urlParts = pageUrl.split('/');
            var start = parseInt(urlParts[urlParts.length - 2]); // before last
            var backwardCount = parseInt(urlParts[urlParts.length - 1]); // last

            return entryId > start - backwardCount && entryId <= start;
        };
        Entry.max = function (array) {
            if (array.length === 0)
                throw 'Cannot get max element in empty array';
            var res = array[0];
            for (var i = 1, l = array.length; i < l; i++) {
                if (Entry.compare(array[i], res) > 0) {
                    res = array[i];
                }
            }
            return res;
        };
    }
};