"use strict";

define(function () {
    return {
        create: function (name, controller, controls) {

            var lastSource = "";

            function setEnabled(control, enabled) {
                if (enabled)
                    control.removeAttr("disabled");
                else
                    control.attr("disabled", "disabled");
            }
            

            function statusChanged(status) {
                controls.status.html(status.status + ($.isNumeric(status.progress) && status.progress != -1 ? ("(" + status.progress.toFixed(1) + "%)") : ""));
                setEnabled(controls.start, status.availableCommands.start);
                setEnabled(controls.stop, status.availableCommands.stop);
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
                    controller.commands.start();
                } else {
                    controller.commands.update(current, controls.emit.attr("checked"));
                }
            }

            return {
                bind: function() {
                    controller.subscribe({ statusChanged: statusChanged, stateChanged: stateChanged, sourceChanged: sourceChanged });
                    controls.start.click(function () { event.preventDefault(); updateAndStart(); });
                    controls.stop.click(function () { event.preventDefault(); controller.commands.stop(); });
                }
            };
        }
    };
});