$(function () {

    var newDataEvent = "es.newStats";
    var timeSeriesClass = "es-time-series";
    var chartTitleClass = "chart-title";
    var appendToSelector = ".wrap";

    buildCharts();

    function buildCharts() {

        $.ajax("/stats?metadata=true&group=false", {
            headers: {
                Accept: "application/json"
            },
            success: success,
            error: error
        });

        function success(stats) {
            var zoomer = prepareZoomer();
            setUpTimeSeries({ zoomer: zoomer });
            bindCharts(stats);
            prepareSelector();
            poll();
        }

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

        function prepareZoomer() {
            var zoomer = new es.Zoomer({
                getNext: function (el) {
                    return getRelativeEl(el, 1);
                },
                getPrev: function (el) {
                    return getRelativeEl(el, -1);
                }
            });
            return zoomer;

            function getRelativeEl(el, offset) {
                var allElems = getAllElems();
                var index = allElems.index(el);
                var relative = allElems[index + offset] || null;
                return relative;
            }
        }

        function prepareSelector() {
            var selector = new es.Selector({
                appendToSelector: ".graphcontrol",
                doAllSelector: ".graphcontrol_nav",
                getTargetElems: getAllElems,
                amendElem: function (sel) {
                    var targetElem = this;
                    $(targetElem).find("." + chartTitleClass)
                                 .append('<a href="" class="hidegraph"><i class="icon-remove"></i></a>')
                                 .click(function (ev) {
                                     ev.preventDefault();
                                     ev.stopPropagation();
                                     sel.updateValue(targetElem, false);
                                 });
                },
                onCheck: function (domElem) {
                    $(domElem).show();
                },
                onUncheck: function (domElem) {
                    $(domElem).hide();
                }
            });
        }

        function setUpTimeSeries(sets) {
            es.TimeSeries.setUp({
                updateEvent: newDataEvent,
                className: timeSeriesClass,
                titleClassName: chartTitleClass,
                appendTo: appendToSelector,
                maxLength: 20,
                zoomer: sets.zoomer
            });
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

            function success(data) {
                $(".error").hide();
                publishNewStat(data);
            }

            function publishNewStat(stat) {
                $(document).trigger(newDataEvent, [stat]);
            };
        };

        function getAllElems() {
            // get all elements with timeseries class inside element to which they were appended
            var allElems = $(appendToSelector + " ." + timeSeriesClass);
            return allElems;
        }

        function skipStatCategory(cat) {
            // todo remove after categories are implemented and queues moved to same page
            if (window.queueStats)
                return cat !== "Queue Stats";
            else
                return cat === "Queue Stats";
        }
    }

    var unloading = false;  // hack around ajax errors
    $(window).bind('beforeunload', function () {
        unloading = true;
    });
});