$(function () {

    // fix of IE caching
    $.ajaxSetup({ cache: false });

    var unloading = false;  // hack around ajax errors
    $(window).bind('beforeunload', function() {
        unloading = true;
    });

    var newDataEvent = "es.newStats";

    var getPollFrequency = function () {
        return 1000;
    };
    var getFreshStatsUrl = function () {
        return "/stats/?format=json";
    };

    es.TimeSeries.setUp({
        updateEvent: newDataEvent,
        appendTo: '.wrap',
        maxLength: 20
    });



    var bindedStats;
    
    if (typeof graphtypeview == "undefined" || graphtypeview == "") {
        bindedStats = es.statsBinding();
    } else if (typeof graphtypeview != "undefined" && graphtypeview == "Queue") {
        bindedStats = es.statsBindingQueue();
    }

    poll();
    function poll() {

        // no matter what - repoll after a while
        setTimeout(function () {
            poll();
        }, getPollFrequency());

        $.getJSON(getFreshStatsUrl())
    		.done(function (stats) {
    		    $(".error").hide();
    		    publishNewStat(stats);
    		})
    		.fail(onFail);
        return;
    };

    function publishNewStat(stat) {
        $(document).trigger(newDataEvent, [stat]);
    };

    function onFail(xhr, status, error) {
        if (unloading)
            return;
        var msg;
        
        if (xhr.status === 0)
            msg = "cannot connect to server";
        else
            msg = "error: " + error;
        $(".error").text(msg).show();
    };
});