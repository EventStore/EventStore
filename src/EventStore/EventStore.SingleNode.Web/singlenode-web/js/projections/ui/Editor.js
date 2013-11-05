"use strict";

define(["ace/ace", "projections/ui/Confirmation", "projections/Observer", "projections/Controller"],
    function (ace, confirmation, observerFactory, controllerFactory) {

    function internalCreate(mode, url, sourceEditor, controls, options) {
        var observer = null;
        var controller = null;

        if (mode === "projection") {
            observer = observerFactory.create(url);
            controller = controllerFactory.create(url, observer);
        }
        else if (mode === "query") {
            if (url) {
                observer = observerFactory.create(url);
                controller = controllerFactory.openQuery(url, observer);
            } else {
                observer = observerFactory.create();
                controller = controllerFactory.createQuery(observer);
            }
        }
        return internalCreate2(url, observer, controller, sourceEditor, controls, options);
    }

    function internalCreate2(url, observer, controller, sourceEditor, controls, options) {
        var lastSource = sourceEditor.getValue();
        var lastEmitEnabled = false;
        var lastStatusUrl = url;
        var lastName = null;

        function setEnabled(control, enabled) {
            if (!control)
                return;
            if (enabled)
                control.removeAttr("disabled");
            else
                control.attr("disabled", "disabled");
        }
            
        function setReadonly(control, readonly) {
            if (!control)
                return;
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
            if (controls.name)
                controls.name.text(status.name);
            if (controls.mode)
                controls.mode.text(status.mode);
            controls.status.text(status.status +
                ($.isNumeric(status.progress) && status.progress != -1 ? ("(" + status.progress.toFixed(1) + "%)") : ""));
            if (status.stateReason) controls.message.show(); else controls.message.hide();
            controls.message.text(status.stateReason);
            setEnabled(controls.start, status.availableCommands.start);
            setEnabled(controls.stop, status.availableCommands.stop);
            setEnabled(controls.save, !options.readonly && status.availableCommands.update);
            setEnabled(controls.debug, status.availableCommands.debug);
            setReadonly(sourceEditor, options.readonly || !status.availableCommands.start);
            if (!status.availableCommands.start)
                sourceEditor.attr("title", "Projection is running");
            else 
                sourceEditor.removeAttr("title");
            if (lastStatusUrl !== status.statusUrl)
                window.location.hash = status.statusUrl;
            lastStatusUrl = status.statusUrl;
            lastName = status.name;
        }

        function urlChanged(url) {
            lastStatusUrl = url;
            lastName = "unknown";
        }

        function stateChanged(state) {
            if (controls.state) 
                controls.state.text(state);
        }

        function resultChanged(state) {
            if (controls.result)
                controls.result.text(state);
        }

        function sourceChanged(source) {

            window.reRenderData = reRenderData;

            function reRenderData(data) {
                renderHtmlBy(data, templateJs, controls.result_stream_container);
            }


            function schedule(dataUrl) {
                setTimeout(function () {
                    $.ajax(dataUrl, {
                        headers: {
                            'Accept': 'application/vnd.eventstore.atom+json',
                        },
                        success: function (d, statusText, jqXHR) {
                            if (jqXHR.status == 200) {
                                reRenderData(d);
                            } else {
                                schedule();
                            }
                        },
                        error: function (jqXhr, status, error) {
                            schedule(dataUrl);
                        }
                    });
                }, 1000);
            }

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
            if (controls.emit)
                controls.emit.attr("checked", source.emitEnabled);
            var anyResults = false;
            if (controls.result_stream) {
                var resultStreamName = source.definition.resultStreamName;
                if (resultStreamName) {
                    controls.result_stream.attr("href", "/streams/" + resultStreamName);
                    controls.result_stream.show();
                    anyResults = true;
                } else {
                    controls.result_stream.hide();
                }
            }

            if (controls.result_stream_container) {

                var resultStreamName = source.definition.resultStreamName;
                if (resultStreamName) {
                    var templateJs = '/web/es/js/atom/FeedElement.html';

                    var dataUrl = '/streams/' + encodeURIComponent(resultStreamName) + "?embed=tryharder";

                    schedule(dataUrl);

                    controls.result_stream_container.show();
                }
                else {
                    controls.result_stream_container.hide();
                }
            }

            if (controls.result) {
                if (!source.definition.byStream && !source.definition.byCustomPartitions) {
                    controls.result.show();
                    anyResults = true;
                } else {

                    controls.result.hide();
                }
            }

            if (controls.result_container)
                if (anyResults)
                    controls.result_container.show();
                else 
                    controls.result_container.hide();

            lastEmitEnabled = source.emitEnabled;
        }

        function updateIfChanged(success) {
            var current = sourceEditor.getValue();
            var emitEnabled = controls.emit && controls.emit.attr("checked");
            if (options.readonly || (lastSource === current && lastEmitEnabled === emitEnabled)) {
                success();
            } else {
                controller.update(current, emitEnabled, success);
            }
        }

        function updateAndStart() {
            var success = controller.start.bind(controller);
            updateIfChanged(success);
        }

        function save() {
            var current = sourceEditor.getValue();
            var emitEnabled = controls.emit && controls.emit.attr("checked");
            controller.update(current, emitEnabled);
        }

        function debug() {
            updateIfChanged(function () {
                window.open("/web/debug-projection.htm#" + lastStatusUrl, "debug-" + lastName);
            });
        }

        function reset() {
            confirmation.confirm("Reset projection?",
                '<div><span class="ui-icon ui-icon-alert" style="float: left; margin: 0 7px 20px 0;"></span>' + 
                '<p style="padding-left: 20px;">Projection reset is an unsafe operation.  Any previously emitted events will be emitted again to the same streams and handled by their subscribers.  <br><strong>Are you sure?</strong></p></div>',
                [
                    {
                        title: "Confirm", handler: function () {
                            controller.reset();
                        }
                    },
                    { title: "Cancel", handler: null },
                ]);
        }

        function stop() {
            controller.stop();
        }

        function bindClick(control, handler) {
            if (!control)
                return;
            control.click(function (event) {
                event.preventDefault();
                if ($(this).attr("disabled"))
                    return;
                handler();
            });
        }

        return {
            bind: function() {
                observer.subscribe({
                    statusChanged: statusChanged,
                    stateChanged: stateChanged,
                    resultChanged: resultChanged,
                    sourceChanged: sourceChanged,
                    urlChanged: urlChanged,
                });
                bindClick(controls.start, updateAndStart);
                bindClick(controls.stop, stop);
                bindClick(controls.save, save);
                bindClick(controls.debug, debug);
                bindClick(controls.reset, reset);
            }
        };
    }

    return {
        createAdvanced: function (url, observer, controller, sourceEditor, controls, options) {
            return internalCreate2(url, observer, controller, sourceEditor, controls, options);
        },

        create: function (url, sourceEditor, controls, options) {
            return internalCreate("projection", url, sourceEditor, controls, options);
        },

        createQuery: function (sourceEditor, controls, options) {
            return internalCreate("query", null, sourceEditor, controls, options);
        },

        openQuery: function (url, sourceEditor, controls, options) {
            return internalCreate("query", url, sourceEditor, controls, options);
        },
    };
});