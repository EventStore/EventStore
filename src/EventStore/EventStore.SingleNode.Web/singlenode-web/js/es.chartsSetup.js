$(function () {

    var newDataEvent = "es.newStats";
    var timeSeriesClass = "es-time-series";
    var chartTitleClass = "es-chart-title-js";
    var appendToSelector = ".wrap";
    var statsStream = '$stats-127.0.0.1:2113'; //todo : calculate properly

    buildCharts();

    function buildCharts() {

        requestStatsMetadata();

        function requestStatsMetadata() {
            $.ajax("/stats?metadata=true&group=false", {    
                headers: {
                    Accept: "application/json"
                },
                success: success,
                error: error
            });
        }

        function success(stats) {
            $(".error").hide().text('');
            var zoomer = prepareZoomer();
            setUpTimeSeries({ zoomer: zoomer });
            bindCharts(stats);
            prepareSelector();
            turnOnProjection();
        }

        function error(xhr, status, err) {
            if (unloading)
                return;
            var msg = es.util.formatError("Couldn't build charts.", xhr);
            $(".error").text(msg).show();
            setTimeout(requestStatsMetadata, 1000);
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
                if (index < 0)
                    return null;
                var relative = allElems[index + offset] || null;
                return relative;
            }
        }

        function prepareSelector() {
            var selector = new es.Selector({
                appendToSelector: ".es-selector-charts",
                getTargetElems: getAllElems,
                amendElem: function (sel) {
                    var targetElem = this;
                    $(targetElem).find("." + chartTitleClass)
                                 .append(
                                     $('<a href="" class="hidegraph"><i class="icon-remove"></i></a>').click(function (ev) {
                                         ev.preventDefault();
                                         ev.stopPropagation();
                                         sel.updateValue(targetElem, false);
                                     })
                                 );
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

        function turnOnProjection() {
            var projection = es.projection({
                body: function () {
                    fromStream(statsStream).when({
                        '$stats-collection': function (state, event) {
                            return event.body;
                        }
                    });
                },
                onStateUpdate: function (state) {
                    var newStats = state;
                    $(document).trigger(newDataEvent, [newStats]);
                },
                showError: function (err) {
//                    alert(err);
                                        if (unloading)
                                            return;
                                        var msg = es.util.formatError("Couldn't update charts.", xhr);
                                        $(".error").text(msg).show();
                }
            });
            projection.start();
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

            function error(xhr, status, err) {

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