"use strict";

define(["ace/ace"], function (ace) {
    return {
        createAceEditor: function (source) {
            var sourceEditor = ace.edit(source.attr("id"));
            sourceEditor.setTheme("ace/theme/chrome");
            sourceEditor.getSession().setMode("ace/mode/javascript");

            sourceEditor.attr = source.attr.bind(source);
            sourceEditor.removeAttr = source.removeAttr.bind(source);
            return sourceEditor;
        }
    };

});