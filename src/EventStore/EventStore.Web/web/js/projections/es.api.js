if (!window.es) { window.es = {}; };
es.postEvent = function (settings) {

    var expectedVersion = settings.expectedVersion || -2;
    var eventId = settings.eventId || guid();
    var correlationId = settings.correlationId || guid();
    var metadata = settings.metadata || "";
    var onError = settings.error || function() {};
    var onSuccess = settings.success || function() {};

    
    var eventType = settings.eventType || (function() { throw "eventType is required"; })();
    var stream = settings.stream || (function() { throw "stream is required"; })();
    var data = settings.data || (function() { throw "data is required"; })();

    var dataStr = null;
    if (typeof data === "object") {
        dataStr = JSON.stringify(data);
    } else if (typeof data === "string") {
        dataStr = data;
    } else {
        throw "couldn't parse data";
    }

    var event = {
        "EventId": eventId,
        "EventType": eventType,
        "Data": dataStr,
        "Metadata": metadata
    };
    var body = {
        "CorrelationId": correlationId,
        "ExpectedVersion": expectedVersion,
        "Events": [event]
    };
    
    var bodyStr = JSON.stringify(body);
    var encodedStream = encodeURIComponent(stream);
    var url = "/streams/" + encodedStream;
    $.ajax(url, {
        type: "post",
        data: bodyStr,
        headers: {
            "Accept": "application/json"
        },
        contentType: "application/json", 
        success: function () {
            onSuccess(eventId, correlationId);
        },
        error: function (xhr, status, error) {
            onError(xhr, eventId, correlationId, expectedVersion);
        }
    });

    function S4() {
        return (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);
    }
    function guid() {
        return (S4() + S4() + "-" + S4() + "-" + S4() + "-" + S4() + "-" + S4() + S4() + S4());
    }
}