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
                    } else {
                        console.log("Ignoring query source changed outside. There are local pending changes.");
                    }
                }
                controls.emit.attr("checked", source.emitEnabled);
            }

            return {
                bind: function() {
                    controller.subscribe({ statusChanged: statusChanged, stateChanged: stateChanged, sourceChanged: sourceChanged });
                    controls.start.click(controller.commands.start);
                    controls.stop.click(controller.commands.stop);
                }
            };
        }
    };
});