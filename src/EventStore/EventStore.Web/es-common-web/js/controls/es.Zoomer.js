if (!window.es) { window.es = {}; };
es.Zoomer = function (sets) {

    var getNext = sets.getNext;
    var getPrev = sets.getPrev;

    this.show = show;

    var current = null;
    var playButton = { text: "play", click: function () { play(); } };
    var stopButton = { text: "stop", click: function () { stop(); } };
    var buttons = [{ text: "prev", click: tryMovePrev },
                   { text: "next", click: tryMoveNext },
                   playButton ];
    var dialog = $(".es-zoomer").dialog({
        autoOpen: false,
        resizable: false,
        width: 750,
        height: 550,
        modal: false,
        closeOnEscape: true,
        buttons: buttons,
        beforeClose: stop,
        position: "center"
    });

    function show(iZoomable) {

        var zoomable = iZoomable.asZoomable();

        var zshow = zoomable.show;
        var cleanUp = zoomable.cleanUp;
        var title = zoomable.title;

        if (current && current.cleanUp)
            current.cleanUp();
        dialog.html("");

        current = zoomable;
        zshow(dialog, 700, 400);

        dialog.dialog("option", "title", title);

        if (!dialog.dialog("isOpen"))
            dialog.dialog("open");

    };

    function tryMovePrev() {
        var prev = getPrev(current.domElem);
        if (prev)
            show(prev);
    }

    function tryMoveNext() {
        var next = getNext(current.domElem);
        if (next)
            show(next);
    }

    var intervalID = null;
    function play() {
        intervalID = setInterval(function () {
            tryMoveNext();
        }, 3000);

        buttons[2] = stopButton;
        dialog.dialog("option", "buttons", buttons);
    }

    function stop() {
        if (intervalID)
            clearInterval(intervalID);

        buttons[2] = playButton;
        dialog.dialog("option", "buttons", buttons);
    }
}