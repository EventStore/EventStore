"use strict";

define([], function () {
    return {
        confirm: function (title, content, buttons) {

            function createButton(v) {
                return function () {
                    $(this).dialog("close");
                    if (v.handler)
                        v.handler();
                };
            }

            if ($("#dialog-confirm").length === 0) {
                $(document.body).append("<div id='dialog-confirm' style='display: none;'></div>");
            }

            var dlg = $("#dialog-confirm");
            dlg.attr("title", title);
            dlg.html(content);

            var dlgButtons = {};

            for (var i in buttons) {
                var v = buttons[i];
                dlgButtons[v.title] = createButton(v);
            }

            $("#dialog-confirm").dialog({
                modal: true,
                buttons: dlgButtons,
            });
        }
    };

});