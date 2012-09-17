(function () {

    poll();
    function poll() {

        // no matter what - repoll after a while
        setTimeout(function () {
            poll();
        }, 1000);

        $.getJSON("/stats/?format=json")
    		.done(function (stats) {
    		    $(".error").hide();
    		    updateTable(stats);
    		})
    		.fail(onFail);
        return;
    };

    function onFail(jqXHR, status, error) {
        $(".error").text(status + " on server. " + error).show();
    };

    var tableBuilt = false;
    function updateTable(stats) {

        var queues = stats.es.queue;

        if (!tableBuilt) {
            tableBuilt = true;
            buildTable(queues);
        }

        $(".queue-stats tbody tr").each(function (i) {
            var queueName = $(this).attr("data-queuename");
            var queue = queues[queueName];

            var columns = $("td", this);
            columns.eq(0).text(queueName);
            columns.eq(1).text(queue.length);
            columns.eq(2).text(queue.avgItemsPerSecond);
            columns.eq(3).text(format(queue.avgProcessingTime, 3));
            columns.eq(4).text(format(queue.idleTimePercent, 1));
            columns.eq(5).text(queue.lengthCurrentTryPeak);
            columns.eq(6).text(queue.lengthLifetimePeak);
            columns.eq(7).text(queue.totalItemsProcessed);
        });        

        function format (number, precision) {
            if (precision < 1) {
                throw "precision must be positive number";
            }

            var divide = Math.pow(10, precision);

            var temp = Math.round(number * divide);
            if (temp == Infinity) {
                return number;
            }

            return temp / divide;
        };


    };

    function buildTable(queues) {

        var sb = [];
        var queueName = null;

        sb.push("<table class='table table-bordered table-striped queue-stats'>");
        sb.push(   "<thead>",
                        "<tr>",
                            _buildRow(["Name",
                                      "Length",
                                      "Avg (items/s)",
                                      "Avg Proccessing Time (ms/item)",
                                      "Idle Time %",
                                      "Peak",
                                      "Max Peak",
                                      "Total Processed"], 'th'),
                        "</tr>",
                    "</thead>",
                    "<tbody>");

        for (queueName in queues) {
            sb.push("<tr data-queuename='", queueName, "'>",
                        "<td class='queuename'>", queueName, "</td>",
                        _buildRow([" ",
                                   " ",
                                   " ",
                                   " ",
                                   " ",
                                   " ",
                                   " "], 'td'),
                    "</tr>");
        }

        sb.push("</tbody>", "</table>");

        var html = sb.join("");

        $(".queue-stats-container").append(html);

        function _buildRow(celldata, celltype) {
            var sbr = [];
            for (var arg in celldata) {
                sbr.push("<" + celltype + ">", celldata[arg], "</" + celltype + ">");
            }
            return sbr.join("");
        };
        
    }

})();