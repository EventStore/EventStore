
(function () {

    var projectionStatusUrl = "";

    var sourceLoaded = false;
    var eventsLoaded = false;
    var eventsRaw = null;

    $(function () {

        $.templates("editSourceTemplate", "#editSourceTemplate");
        projectionStatusUrl = location.hash.substr(1);

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

        requestProjectionDebugState();
        requestProjectionSource();
    });

    function requestProjectionDebugState() {
        $.ajax(projectionStatusUrl + "/debug", {
            headers: {
                Accept: "application/json",
            },
            dataType: "text",
            success: successDebugState,
            error: errorDebugState
        });

        function successDebugState(data, status, xhr) {
            $("#projection-debug-events").text(data);
            eventsRaw = data;
            eventsLoaded = true;
            checkLoaded();
        }

        function errorDebugState(xhr, status) {
            setTimeout(requestProjectionDebugState, 1000);
        }
    }

    function requestProjectionSource() {
        $.ajax(projectionStatusUrl + "/query", {
            headers: {
                Accept: "text/plain",
            },
            dataType: "text",
            success: successSource,
            error: errorSource
        });

        function successSource(data, status, xhr) {
            $("#source").text(data);
            sourceLoaded = true;
            checkLoaded();
        }

        function errorSource(xhr, status) {
            setTimeout(requestProjectionSource, 1000);
        }
    }

    function successUpdateSource(data, status, xhr) {
        window.location = "view-projection.htm#" + projectionStatusUrl;
    }

    function checkLoaded() {
        if (sourceLoaded && eventsLoaded) {
            readyForDebugging();
        }
    }

    function readyForDebugging() {
        var first = JSON.parse(eventsRaw)[0];

        document.getElementById('script-placeholder').contentDocument.write(
            '<div id="text"></div>' +
                '<script src="/web/es/js/projections/v8/Prelude/Modules.js"><' + '/script>' +
                '<script src="/web/es/js/projections/v8/Prelude/Projections.js"><' + '/script>' +
                '<script src="/web/es/js/projections/es.projections.environment.js"><' + '/script>' +
                '<script src="/web/es/js/projections/v8/Prelude/1Prelude.js"><' + '/script>' +
                '<script src="/web/es/js/projections/es.projection.js"><' + '/script>' +
                '<script src="/web/es/js/projections/es.api.js"><' + '/script>' +
                '<script>window.processor = $initialize_hosted_projections(); processor.set_debugging();<' + '/script>' +
                '<script src="' + projectionStatusUrl + '/query?1"><' + '/' + 'script>');


        $.ajax(projectionStatusUrl + "/state?partition=" + first.Partition, {
            headers: {
                Accept: "application/json",
            },
            dataType: "text",
            success: successPartitionState,
            error: errorPartitionState
        });

        function successPartitionState(data, status, xhr) {
            $("#projection-debug-state").text(data);
            $('#run-button').removeAttr("disabled");

            $('#run-button').click(function () {
                var processor = document.getElementById('script-placeholder').contentWindow.processor;
                if (data == "")
                    processor.initialize();
                else
                    processor.set_state(data);
                processor.process_event(
                    first.BodyRaw,
                    first.StreamId,
                    first.EventType,
                    first.Category,
                    first.SequenceNumber,
                    first.MetadataRaw,
                    first.LogPposition);
            });

        }

        function errorPartitionState(xhr, status) {
            alert("Failed to get state for partition: " + first.Partition);
        }

    }

})();

