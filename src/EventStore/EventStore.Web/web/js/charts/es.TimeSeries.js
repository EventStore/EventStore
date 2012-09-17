(function() {

    if (!window.es) { window.es = {}; };
    window.es.TimeSeries = function (sets) {
    
        var title = sets.title;
        var updateEventName = sets.updateEvent || es.TimeSeries.updateEvent;
        var getData = sets.getData || function(data) { return data; };
        var appendToElement = sets.appendTo || es.TimeSeries.appendTo || '.content';
        var maxLength = sets.maxLength || 100;
        
        var container = $('<div class="chart-cont" />')
                            .append( ["<div class='chart-title'>", title, "</div>"].join("") )
                            .appendTo(appendToElement)
                            .click(_handleZoom);							
    
        var seriesData = [];
        _initData();
        
        var graph = _createGraph(container, seriesData, 300, 100);

        $(document).on(updateEventName, function(event, data) {
            try {
                var ownData = getData(data);
            } catch(e) {
                return;
            } 
            
            var newPoint =  {x : (new Date()).getTime() / 1000 , y : ownData };
            seriesData.push(newPoint);
            
            if (seriesData.length > maxLength) {
                seriesData.shift();
            }

            graph.update();
        });
        
        function _initData() {
            // fills data with n initial values so that it looks like chart is floating to the left, not transforming
            for (var i = maxLength - 1; i > 0; i--) {
                seriesData.push({ x: (new Date()).getTime() / 1000 - i, y: 0 });                   
            };
        };

        function _createGraph(appendTo, data, width, height) {
        
            var graph = new Rickshaw.Graph( {
                element: $('<div class="chart" />').appendTo(appendTo)[0],
                width: width,
                height: height,
                renderer: 'area',
                stroke: true,
                interpolation: "cardinal",
                series: [{
                        color: '#C9E63C',
                        data: data,
                        name: title
                    }
                ]
            });
            
            graph.render();
            
            var slider = new Rickshaw.Graph.RangeSlider( {
                graph: graph,
                element: $('<div class="slider" />').appendTo(appendTo)
            } );

           // slider.element.slider("option", "values", [maxLength - 20, maxLength]);
            
            var hoverDetail = new Rickshaw.Graph.HoverDetail( {
                graph: graph
            } );
            
            var ticksTreatment = 'glow';
            
            var xAxis = new Rickshaw.Graph.Axis.Time( {
                graph: graph,
                ticksTreatment: ticksTreatment
            } );
            
            xAxis.render();
            
            var yAxis = new Rickshaw.Graph.Axis.Y( {
                graph: graph,
                tickFormat: Rickshaw.Fixtures.Number.formatKMBT,
                ticksTreatment: ticksTreatment,
                pixelsPerTick : 35
            } );
            
            yAxis.render();
            
            return graph;
        };

        function _handleZoom() {
            var dialog = $(".zoomed-chart").html("");
            var graph = _createGraph(dialog, seriesData, 950, 600);

            dialog.dialog({
                title: title,
                resizable: false,
                width: 1024,
                height: 750,
                modal: true,
                closeOnEscape: true,
                buttons: [
                    {
                        text: "prev",
                        click: function() { _dialognav("p", title); }
                    },
                    {
                        text: "next",
                        click: function() { _dialognav("n", title); }
                    },
                    {
                        text: "play/stop",
                        click: function () { _playstop(); }
                    }
                ],
                open: _initbutton,
                beforeClose: function () { _playstop(1); },
                position: "center"
            });

            $(document).on(updateEventName, function(event, data) {
                graph.update();
            });

            function _dialognav(action, title){
                var allgraph = $(".chart-cont"),
                    allgraphtitle = [];

                $(allgraph).find(".chart-title").each(function(index){
                    allgraphtitle[index] = $(this).text();
                });

                var  selectgraph = jQuery.inArray(title, allgraphtitle);

                if(action == "p"){
                    title = $(allgraph).eq(selectgraph).prevAll(".chart-cont:visible").eq(0).find(".chart-title").text();
                    selectgraph = jQuery.inArray(title, allgraphtitle);
                    if(selectgraph == -1){
                        title = $(allgraph).nextAll(".chart-cont:visible").last().find(".chart-title").text();
                        selectgraph = jQuery.inArray(title, allgraphtitle);
                    }
                    $(allgraph).eq(selectgraph).click();
                }
                else if(action == "n"){
                    title = $(allgraph).eq(selectgraph).nextAll(".chart-cont:visible").eq(0).find(".chart-title").text();
                    selectgraph = jQuery.inArray(title, allgraphtitle);
                    if(selectgraph == -1){
                        title = $(allgraph).prevAll(".chart-cont:visible").last().find(".chart-title").text();
                        selectgraph = jQuery.inArray(title, allgraphtitle);
                    }

                    $(allgraph).eq(selectgraph).click();

                }
                _initbutton();
            }; /*_dialognav*/


            function _playstop(ifclose) {
                if (typeof (intervalID) != "undefined" && intervalID != 0) {
                    clearInterval(intervalID);
                    intervalID = 0;
                } else {
                    if (ifclose != 1) {
                        intervalID = setInterval(function () {
                            $(".ui-dialog-buttonpane button#next").click();
                        }, 3000);
                    }
                }
            };/*_playstop*/

            function _initbutton(){
                $(".ui-dialog-buttonpane button").each(function(){
                    $(this).attr("id", $(this).text());
                });
            };

        }; /*_handleZoom*/

    };
    
    window.es.TimeSeries.setUp = function(sets){
        $.extend(window.es.TimeSeries, sets);
    };
    
})();