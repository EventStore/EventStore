$(function () {



    var newDataEvent = "es.newStats";

    es.TimeSeries.setUp({
        updateEvent: newDataEvent,
        appendTo: '.wrap',
        maxLength: 20
    });


    buildCharts();

    function buildCharts() {

        $.ajax("/stats?metadata=true&group=false", {
            headers: {
                Accept: "application/json"
            },
            success: onGotStats,
            error: error
        });

        function onGotStats(stats) {
            bindCharts(stats);
            poll();
        }

        function bindCharts(stats) {
            for (var statName in stats) {
                (function () {
                    var currentStatName = statName; // closure
                    var stat = stats[currentStatName];
                    if (stat && stat.drawChart && !skipStatCategory(stat.category)) {
                        es.TimeSeries({
                            title: stat.title,
                            getData: function (data) {
                                return data[currentStatName];
                            }
                        });

                    }
                })();
            }
        }

        function skipStatCategory(cat) {
            // todo remove after categories are implemented and queues moved to same page
            if (window.queueStats)
                return cat !== "Queue Stats";
            else
                return cat === "Queue Stats"; 
        }
    }

    function poll() {

        // no matter what - repoll after a while
        setTimeout(function () {
            poll();
        }, 1000);

        $.ajax("/stats?group=false", {
            headers: {
                Accept: "application/json"
            },
            success: success,
            error: error
        });
    };

    function success(data) {
        $(".error").hide();
        publishNewStat(data);
    }

    function publishNewStat(stat) {
        $(document).trigger(newDataEvent, [stat]);
    };

    function error(xhr, status, err) {
        if (unloading)
            return;
        var msg;

        if (xhr.status === 0)
            msg = "cannot connect to server";
        else
            msg = "error: " + err;
        $(".error").text(msg).show();
    };

    var unloading = false;  // hack around ajax errors
    $(window).bind('beforeunload', function () {
        unloading = true;
    });
});