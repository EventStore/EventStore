"use strict";

define(["ace/ace"], function (ace) {
    return {
        create: function (name, observer, controller, controls) {

            var sourceEditor = ace.edit(controls.source.attr("id"));
            sourceEditor.setTheme("ace/theme/chrome");
            sourceEditor.getSession().setMode("ace/mode/javascript");

            sourceEditor.attr = controls.source.attr.bind(controls.source);
            sourceEditor.removeAttr = controls.source.removeAttr.bind(controls.source);


            var lastSource = "";
            var lastEmitEnabled = false;

            function setEnabled(control, enabled) {
                if (enabled)
                    control.removeAttr("disabled");
                else
                    control.attr("disabled", "disabled");
            }
            
            function setReadonly(control, readonly) {
                if (control.setReadOnly) {
                    control.setReadOnly(readonly);
                } else {
                    if (readonly)
                        control.removeAttr("readonly");
                    else
                        control.attr("readonly", "readonly");
                }
            }


            function statusChanged(status) {
                controls.name.text(status.name);
                controls.status.text(status.status +
                    ($.isNumeric(status.progress) && status.progress != -1 ? ("(" + status.progress.toFixed(1) + "%)") : ""));
                if (status.stateReason) controls.message.show(); else controls.message.hide();
                controls.message.text(status.stateReason);
                setEnabled(controls.start, status.availableCommands.start);
                setEnabled(controls.stop, status.availableCommands.stop);
                setEnabled(controls.save, status.availableCommands.update);
                setReadonly(sourceEditor, !status.availableCommands.start);
                if (!status.availableCommands.start)
                    sourceEditor.attr("title", "Projection is running");
                else 
                    sourceEditor.removeAttr("title");
            }

            function stateChanged(state) {
                controls.state.text(state);
            }

            function sourceChanged(source) {
                var current = sourceEditor.getValue();
                if (current !== source.query) {
                    if (lastSource === current) {
                        sourceEditor.setValue(source.query);
                        sourceEditor.navigateFileStart();
                        lastSource = source.query;
                    } else {
                        console.log("Ignoring query source changed outside. There are local pending changes.");
                    }
                }
                controls.emit.attr("checked", source.emitEnabled);
                lastEmitEnabled = source.emitEnabled;
            }

            function updateAndStart() {
                var current = sourceEditor.getValue();
                var emitEnabled = !!controls.emit.attr("checked");
                if (lastSource === current && lastEmitEnabled === emitEnabled) {
                    controller.start();
                } else {
                    controller.update(current, emitEnabled, controller.start.bind(controller));
                }
            }

            function save() {
                var current = sourceEditor.getValue();
                var emitEnabled = !!controls.emit.attr("checked");
                controller.update(current, emitEnabled);
            }

            function stop() {
                controller.stop();
            }

            function bindClick(control, handler) {
                control.click(function(event) {
                    event.preventDefault();
                    if ($(this).attr("disabled"))
                        return;
                    handler();
                });
            }

            return {
                bind: function() {
                    observer.subscribe({ statusChanged: statusChanged, stateChanged: stateChanged, sourceChanged: sourceChanged });
                    bindClick(controls.start, updateAndStart);
                    bindClick(controls.stop, stop);
                    bindClick(controls.save, save);
                }
            };
        }
    };
});