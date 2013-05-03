
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

        $(function () {

            $.templates("editSourceTemplate", "#editSourceTemplate");
            projectionStatusUrl = location.hash.substr(1);
            console.log(projectionStatusUrl);

            var observer = observerFactory.create(projectionStatusUrl, { autoRefresh: false });


            observer.subscribe({
                statusChanged: function (status) {
                    if (status.status !== "Stopped" && status.status !== "Faulted")
                        alert("The projection must be stopped to be debugged");
                    projectionStatusOk = true;
                    checkLoaded();
                },
                stateChanged: function (state, xhr) {
                    var positionJson = xhr.getResponseHeader("X-ES-Position");
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
            observer.poll();

            function partLoaded() {
                if (projectionStatusOk && projectionPosition && projectionDefintion && projectionSource) {
                    loadEvents();
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
                    success: function (data) {
                        $("#projection-debug-events").text(data);
                        eventsRaw = JSON.parse(data).events;
                        eventsLoaded = true;
                        checkLoaded();
                    },
                    error: function (xhr) {
                        setTimeout(loadEvents, 1000);
                    }
                });
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

        });


        function successUpdateSource(data, status, xhr) {
            window.location = "view-projection.htm#" + projectionStatusUrl;
        }

        function checkLoaded() {
            if (eventsLoaded) {
                readyForDebugging();
            }
        }

        function readyForDebugging() {
            var first = eventsRaw[0];

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

            function loadState() {

                var processor = document.getElementById('script-placeholder').contentWindow.processor;
                if (!processor) {
                    setTimeout(loadState, 100);
                    return;
                }

                var partition = null;
                if (projectionDefintion.byCustomPartitions)
                    partition = processor.get_state_partition(
                        first.isJson ? JSON.stringify(first.data) : first.data,
                        first.eventStreamId,
                        first.eventType,
                        first.category,
                        first.eventNumber,
                        first.isJson ? JSON.stringify(first.metadata) : first.metadata);
                else if (projectionDefintion.byStreams)
                    partition = first.eventStreamId;
                else
                    partition = "";

                $.ajax(projectionStatusUrl + "/state?partition=" + partition, {
                    headers: {
                        Accept: "application/json",
                    },
                    dataType: "text",
                    success: successPartitionState,
                    error: errorPartitionState
                });

                function successPartitionState(data, status, xhr) {
                    $('#run-button').removeAttr("disabled");
                    if (data == "")
                        processor.initialize();
                    else
                        processor.set_state(data);
                    $("#projection-debug-result").text(data);
                }

                function errorPartitionState(xhr, status) {
                    alert("Failed to get state for partition: " + first.partition);
                }

                $('#run-button').click(function() {
                    $('#run-button').attr("disabled", "disabled");
                    processor.process_event(
                        first.isJson ? JSON.stringify(first.data) : first.data,
                        first.isJson,
                        first.eventStreamId,
                        first.eventType,
                        first.category,
                        first.eventNumber,
                        first.isJson ? JSON.stringify(first.metadata) : first.metadata,
                        partition);

                });

            }

        }
    }

);
