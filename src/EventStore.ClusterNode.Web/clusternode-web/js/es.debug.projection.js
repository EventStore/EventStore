
require.config({ baseUrl: "/web/es/js" });
require(['projections/Observer', 'projections/Controller'],
    function (observerFactory, controllerFactory) {

        var projectionStatusUrl = "";

        var sourceLoaded = false;
        var eventsLoaded = false;
        var eventsRaw = null;
        var projectionPosition = null;
        var projectionDefintion = null;
        var projectionSource = null;
        var projectionStatusOk = null;
        var cachedStates = {};
        var projectionInitialized = false;
        var processor = null;
        var currentEvent = null;
        var partition = null;

        $(function () {

            $.templates("editSourceTemplate", "#editSourceTemplate");
            projectionStatusUrl = location.hash.substr(1);
            console.log(projectionStatusUrl);

            var observer = observerFactory.create(projectionStatusUrl, { autoRefresh: false });


            observer.subscribe({
                statusChanged: function (status) {
                    if (status.status !== "Stopped" && status.status !== "Faulted")
                        alert("The projection must be stopped to be debugged. " + status.status);
                    projectionStatusOk = true;
                    checkLoaded();
                },
                stateChanged: function (state, xhr) {
                    var positionJson = xhr.getResponseHeader("ES-Position");
                    var position = JSON.parse(positionJson);
                    projectionPosition = position;
                    partLoaded();
                },
                sourceChanged: function (source) {
                    projectionDefintion = source.definition;
                    projectionSource = source.query;
                    $("#source").text(projectionSource);
                    partLoaded();
                }
            });
            updateStatus("Loading definition...");
            observer.poll();
            updateStatus("");

            function partLoaded() {
                if (projectionStatusOk && projectionPosition && projectionDefintion && projectionSource) {
                    loadEvents();
                }
            }

            function onLoadEventsSuccess(data) {
                $("#projection-debug-events").text(data);
                eventsRaw = JSON.parse(data).events;
                if (eventsRaw && eventsRaw.length > 0) {
                    eventsLoaded = true;
                    checkLoaded();
                } else {
                    updateStatus("No further events are available.  Waiting...");
                    setTimeout(loadEvents, 1000);
                }
            }

            function loadEvents() {
                $.ajax("/projections/read-events", {
                    headers: {
                        Accept: "application/json",
                    },
                    contentType: "application/json",
                    dataType: "text",
                    type: "POST",
                    data: JSON.stringify({
                        query: projectionDefintion,
                        position: projectionPosition,
                        maxEvents: 10,
                    }),
                    success: onLoadEventsSuccess,
                    error: function (xhr) {
                        setTimeout(loadEvents, 1000);
                    }
                });
            }

            function successUpdateSource(data, status, xhr) {
                window.location = "view-projection.htm#" + projectionStatusUrl;
            }

            function checkLoaded() {
                if (eventsLoaded) {
                    readyForDebugging();
                }
            }

            function partitionStateLoaded(data) {
                updateStatus("Ready for debugging!");
                $('.run-button').removeAttr("disabled");
                if (data == "") {
                    processor.initialize();
                    cachedStates[partition] = processor.debugging_get_state();
                } else
                    processor.set_state(data);
                $("#projection-debug-result").text(cachedStates[partition]);
            }

            function readyForDebugging() {
                currentEvent = eventsRaw[0];

                if (!projectionInitialized) {

                    updateStatus("Running the definition...");

                    document.getElementById('script-placeholder').contentDocument.write(
                        '<div id="text"></div>' +
                            '<script src="/web/es/js/projections/v8/Prelude/Modules.js"><' + '/script>' +
                            '<script src="/web/es/js/projections/v8/Prelude/Projections.js"><' + '/script>' +
                            '<script src="/web/es/js/projections/es.projections.environment.js"><' + '/script>' +
                            '<script src="/web/es/js/projections/v8/Prelude/1Prelude.js"><' + '/script>' +
                            '<script src="/web/es/js/projections/es.projection.js"><' + '/script>' +
                            '<script src="/web/es/js/projections/es.api.js"><' + '/script>' +
                            '<script>window.processor = $initialize_hosted_projections(); processor.set_debugging();<' + '/script>' +
                            '<script src="' + projectionStatusUrl + '/query"><' + '/' + 'script>');

                    setTimeout(loadState, 100);
                } else {
                    loadState();
                }

                function loadState() {

                    processor = document.getElementById('script-placeholder').contentWindow.processor;
                    if (!processor) {
                        setTimeout(loadState, 100);
                        return;
                    }

                    projectionInitialized = true;

                    updateStatus("");

                    partition = null;
                    console.log(projectionDefintion);
                    if (projectionDefintion.byCustomPartitions)
                        partition = processor.get_state_partition(
                            currentEvent.isJson ? JSON.stringify(currentEvent.data) : currentEvent.data,
                            currentEvent.eventStreamId,
                            currentEvent.eventType,
                            currentEvent.category,
                            currentEvent.eventNumber,
                            currentEvent.isJson ? JSON.stringify(currentEvent.metadata) : currentEvent.metadata);
                    else if (projectionDefintion.byStream)
                        partition = currentEvent.eventStreamId;
                    else
                        partition = "";

                    $("#partition").text(partition);

                    if (cachedStates[partition]) {
                        partitionStateLoaded(cachedStates[partition]);
                    } else {

                        updateStatus("Loading the projection state...");

                        $.ajax(projectionStatusUrl + "/state?partition=" + partition, {
                            headers: {
                                Accept: "application/json",
                            },
                            dataType: "text",
                            success: successPartitionState,
                            error: errorPartitionState
                        });

                        function successPartitionState(data, status, xhr) {
                            partitionStateLoaded(data);
                        }

                        function errorPartitionState(xhr, status) {
                            updateStatus("Error loading the projection state");
                        }

                    }
                }

            }

            function updateStatus(status) {
                if (status !== "")
                    $("#status").text(" - " + status);
                else
                    $("#status").text("");
            }

            function runProjectionStep() {
                var state = processor.process_event(
                    currentEvent.isJson ? JSON.stringify(currentEvent.data) : currentEvent.data,
                    currentEvent.isJson,
                    currentEvent.eventStreamId,
                    currentEvent.eventType,
                    currentEvent.category,
                    currentEvent.eventNumber,
                    currentEvent.isJson ? JSON.stringify(currentEvent.metadata) : currentEvent.metadata,
                    partition);


                cachedStates[partition] = state;
                console.log(currentEvent.readerPosition);
                projectionPosition = currentEvent.readerPosition;
                eventsLoaded = false;
                loadEvents();
            }

            $('#update-button').click(function (ev) {
                $.ajax(projectionStatusUrl + "/query", {
                    headers: {
                        Accept: "application/json",
                    },

                    type: "PUT",
                    data: $("#source").val(),
                    success: successUpdateSource,
                    error: function (xhr) {
                        var msg = es.util.formatError("Couldn't delete projection", xhr);
                        alert(msg);
                    }
                });
            });
            $('.run-button').click(function () {
                $('.run-button').attr("disabled", "disabled");
                runProjectionStep();
            });


        });


    }

);
