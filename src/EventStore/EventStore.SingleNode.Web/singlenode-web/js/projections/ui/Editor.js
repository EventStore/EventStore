"use strict";

define(function () {
    return {
        create: function (name, observer, controller, controls) {

            var lastSource = "";

            function setEnabled(control, enabled) {
                if (enabled)
                    control.removeAttr("disabled");
                else
                    control.attr("disabled", "disabled");
            }
            
            function setReadonly(control, readonly) {
                if (readonly)
                    control.removeAttr("readonly");
                else
                    control.attr("readonly", "readonly");
            }


            function statusChanged(status) {
                controls.name.text(status.name);
                controls.status.text(status.status +
                    ($.isNumeric(status.progress) && status.progress != -1 ? ("(" + status.progress.toFixed(1) + "%)") : ""));
                if (status.stateReason) controls.message.show(); else controls.message.hide();
                controls.message.text(status.stateReason);
                setEnabled(controls.start, status.availableCommands.start);
                setEnabled(controls.stop, status.availableCommands.stop);
                setReadonly(controls.source, status.availableCommands.start);
                if (!status.availableCommands.start)
                    controls.source.attr("title", "Projection is running");
                else 
                    controls.source.removeAttr("title");
            }

            function stateChanged(state) {
                controls.state.text(state);
            }

            function sourceChanged(source) {
                var current = controls.source.text();
                if (current !== source.query) {
                    if (lastSource === current) {
                        controls.source.text(source.query);
                        lastSource = source.query;
                    } else {
                        console.log("Ignoring query source changed outside. There are local pending changes.");
                    }
                }
                controls.emit.attr("checked", source.emitEnabled);
            }

            function updateAndStart() {
                var current = controls.source.val();
                if (lastSource === current) {
                    controller.start();
                } else {
                    controller.update(current, controls.emit.attr("checked"), controller.start.bind(controller));
                }
            }

            return {
                bind: function() {
                    observer.subscribe({ statusChanged: statusChanged, stateChanged: stateChanged, sourceChanged: sourceChanged });
                    controls.start.click(function (event) { event.preventDefault(); updateAndStart(); });
                    controls.stop.click(function (event) { event.preventDefault(); controller.stop(); });
                }
            };
        }
    };
});