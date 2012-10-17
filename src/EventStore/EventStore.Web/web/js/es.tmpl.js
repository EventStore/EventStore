if (!window.es) { window.es = {}; };
es.tmpl = (function () {

    return {
        renderHead: renderHead,
        renderBody: renderBody
    };

    function renderHead() {
        renderTemplate("head", "#r-head", null);
    }

    function renderBody(opts) {
        registerOnLoad();

        var $content = $("#content");
        var content = $content.html();
        $content.remove();
        var data = $.extend({}, opts, {
            content: content
        });
        renderTemplate("body", "#r-body", data);
    }

    function renderTemplate(tmplName, targetSelector, data) {
        var file = formatTemplatePath(tmplName);
        $.get(file, null, function (template) {
            var tmpl = $.templates(template);
            var htmlString = tmpl.render(data);
            if (targetSelector) {
                $(targetSelector).replaceWith(htmlString);
            }
            return htmlString;
        });
    }

    function formatTemplatePath(name) {
        return "tmpl/_" + name + ".tmpl.html";
    }

    function registerOnLoad() {
        var jqueryBackup = window.$;
        if (typeof jqueryBackup == "undefined")
            throw "jQuery msut be defined before register es-specific onload";

        jqueryBackup.extend($esload, jqueryBackup);

        window.$ = $esload;
        window.__$esdoload = load;
        var isLoaded = false;
        var toLoad = [];

        function $esload(onload) {
            if (typeof onload !== "function") {
                // call whatever was supposed to be done with jquery
                return jqueryBackup.apply(window, arguments);
            }

            if (isLoaded)
                onload();
            else
                toLoad.push(onload);
        }
        function load() {
            isLoaded = true;
            for (var i in toLoad)
                toLoad[i]();
        }
    }
})();
