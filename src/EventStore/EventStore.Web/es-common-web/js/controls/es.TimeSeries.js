(function () {

    if (!window.es) { window.es = {}; };
    window.es.TimeSeries = function (sets) {

        var title = sets.title;
        var titleSuffix = sets.titleSuffix || "";
        var titleClassName = sets.titleClassName || es.TimeSeries.titleClassName;
        var updateEventName = sets.updateEvent || es.TimeSeries.updateEvent;
        var appendToElement = sets.appendTo || es.TimeSeries.appendTo;
        var getData = sets.getData || function (data) { return data; };
        var maxLength = sets.maxLength || es.TimeSeries.maxLength || 100;
        var className = sets.className || es.TimeSeries.className || "";
        var zoomer = sets.zoomer || es.TimeSeries.zoomer;
        var width = sets.width || es.TimeSeries.width || 300;
        var height = sets.height || es.TimeSeries.height || 100;

        var seriesData = [];
        var container = null;


        init();

        function init() {

            container = $('<div class="es-chart-cont ' + className + '" />')
                            .append(["<div class='es-chart-title ", titleClassName, "' style='max-width:", width, "px'>", title, titleSuffix, "</div>"].join(""))
                            .appendTo(appendToElement)
                            .click(handleZoom);
            container[0].asZoomable = asZoomable;
            container[0].asSelectable = asSelectable;

            initData(new Date());

            var graph = createGraphInternal(container, seriesData, width, height);
            $(document).on(updateEventName, function (event, data, timeStamp) {
                onNewData(data, timeStamp);
                graph.update();
            });
        }

        function initData(timeStamp) {
            // fills data with n initial values so that it looks like chart is floating to the left, not transforming
            for (var i = Math.min(maxLength, 50) - 1; i > 0; i--) {
                seriesData.push({ x: (timeStamp).getTime() / 1000 - i, y: 0 });
            };
        };

        function onNewData(data, timeStamp) {
            try {
                var ownData = getData(data);
            } catch (e) {
                return;
            }

            timeStamp = timeStamp || new Date();

            var newPoint = { x: timeStamp.getTime() / 1000, y: ownData };
            seriesData.push(newPoint);

            if (seriesData.length > maxLength) {
                seriesData.shift();
            }
        }

        function createGraphInternal(appendTo, data, width, height) {

            var graph = new Rickshaw.Graph({
                element: $('<div class="chart" />').appendTo(appendTo)[0],
                width: width,
                height: height,
                renderer: 'area',
                stroke: true,
                interpolation: "cardinal",
                padding: {
                    top: 0.2
                },
                series: [{
                    color: '#C9E63C',
                    data: data,
                    name: title
                }
                ]
            });

            graph.render();

            var slider = new Rickshaw.Graph.RangeSlider({
                graph: graph,
                element: $('<div class="slider" />').appendTo(appendTo)
            });

            // slider.element.slider("option", "values", [maxLength - 20, maxLength]);

            var hoverDetail = new Rickshaw.Graph.HoverDetail({
                graph: graph
            });

            var ticksTreatment = 'glow';

            var xAxis = new Rickshaw.Graph.Axis.Time({
                graph: graph,
                ticksTreatment: ticksTreatment
            });

            xAxis.render();

            var yAxis = new Rickshaw.Graph.Axis.Y({
                graph: graph,
                tickFormat: Rickshaw.Fixtures.Number.formatKMBT,
                ticksTreatment: ticksTreatment,
                pixelsPerTick: 35
            });

            yAxis.render();

            return graph;
        };

        function asZoomable() {

            var graph = null;
            var onUpdate = function () { graph.update(); };

            var show = function (appendTo, width, height) {
                graph = createGraphInternal(appendTo, seriesData, width, height);
                $(document).on(updateEventName, onUpdate);
            };

            var cleanUp = function () {
                $(document).off(updateEventName, onUpdate);
                graph = null;
            };

            return {
                show: show,
                cleanUp: cleanUp,
                title: title + titleSuffix,
                domElem: container[0]
            };
        }

        function asSelectable() {
            return {
                title: title,
            }; 
        }

        function handleZoom() {
            zoomer.show(container[0]);
        }
    };

    window.es.TimeSeries.setUp = function (sets) {
        $.extend(window.es.TimeSeries, sets);
    };

})();