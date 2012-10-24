if (!window.es) { window.es = {}; };
es.tmpl = (function () {

    var templatesRequests = [];

    return {
        renderHead: renderHead,
        renderBody: renderBody,
        render: render
    };

    function renderHead() {
        renderInternal("head", "#r-head", null);
    }

    function renderBody(opts) {
        registerOnLoad();

        var $content = $("#content");
        var content = $content.html();
        $content.remove();
        var data = $.extend({}, opts, {
            content: content
        });
        renderInternal("body", "#r-body", data);
    }

    function render(tmplName, targetSelector, data, templatesToWait) {

        $(targetSelector).html('');

        templatesToWait = templatesToWait || [];
        tryAddToArray('body', templatesToWait);
        tryAddToArray('head', templatesToWait);

        var grepped = $.grep(templatesRequests, function (el) { return $.inArray(el.name, templatesToWait) > -1; });
        var toWait = $.map(grepped, function (el) { return el.req; });

        $.when.apply($, toWait)
         .then(function () {
             renderInternal(tmplName, targetSelector, data);
         });
    }

    function renderInternal(tmplName, targetSelector, data) {
        var file = formatTemplatePath(tmplName);
        var ajax = $.get(file, null, function (template) {
            var tmpl = $.templates(template);
            var htmlString = tmpl.render(data);
            if (targetSelector) {
                $(targetSelector).replaceWith(htmlString);
            }
            return htmlString;
        });
        templatesRequests.push({ name: tmplName, req: ajax });
    }

    function formatTemplatePath(name) {
        return "/web/es/tmpl/_" + name + ".tmpl.html";
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

    function tryAddToArray(item, array) {
        var index = jQuery.inArray(item, array);
        if (index < 0) {
            array.push(item);
        }
    }
})();
