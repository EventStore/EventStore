if (!window.es) { window.es = {}; };
es.Zoomer = function (sets) {

    var getNext = sets.getNext;
    var getPrev = sets.getPrev;

    this.show = show;

    var dialog = $(".zoomed-chart");
    var current = null;

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

        dialog.dialog({
            title: title,
            resizable: false,
            width: 750,
            height: 550,
            modal: true,
            closeOnEscape: true,
            buttons: [
                    {
                        text: "prev",
                        click: tryMovePrev
                    },
                    {
                        text: "next",
                        click: tryMoveNext
                    },
                    {
                        text: "play/stop",
                        click: function () {
                            playstop();
                        }
                    }
                ],
            beforeClose: function () { playstop(true); },
            position: "center"
        });

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

        var intervalID;
        function playstop(close) {
            if (typeof (intervalID) != "undefined" && intervalID != 0) {
                clearInterval(intervalID);
                intervalID = 0;
            } else {
                if (!close) {
                    intervalID = setInterval(function () {
                        tryMoveNext()
                    }, 3000);
                }
            }
        };

    };
}