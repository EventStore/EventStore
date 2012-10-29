if (!window.es) { window.es = {}; };
es.tmpl = (function () {

    var templatesToWait = [];
    var templatesToLoadCount = 0;

    var _doLoad = null;
    var isLoaded = false;

    registerOnLoad();

    return {
        renderHead: renderHead,
        renderBody: renderBody,
        render: render
    };

    function renderHead() {
        renderInternal({
            tmplName: "head",
            targetSelector: "#r-head"
        });
    }

    function renderBody(opts) {
        renderInternal({
            tmplName: "body",
            scriptContSelector: "#r-body",
            targetSelector: "#content",
            beforeLoad: function (data) {
                $.extend(data, opts, {
                    content: $("#content").html()
                });
            }
        });
    }

    function render(tmplName, targetSelector, data) {
        renderInternal({
            tmplName: tmplName,
            targetSelector: targetSelector,
            data: data
        });
    }

    function renderInternal(sets) {

        var tmplName = sets.tmplName;
        var targetSelector = sets.targetSelector;
        var scriptContSelector = sets.scriptContSelector || sets.targetSelector;
        var data = sets.data || {};
        var beforeLoad = sets.beforeLoad || function () { };

        templatesToLoadCount++;

        // remove script to avoid executing it for the second time when loading template
        $(scriptContSelector).html('');

        var waitHandleCont = {};
        var wait = createWaitHandle(waitHandleCont);

        // wait for all previous templates to render
        $.when.apply($, templatesToWait)
         .then(function () {
             doRender();
         });

        templatesToWait.push(wait);

        function createWaitHandle(handleCont) {
            return $.Deferred(function (deferredObj) {
                handleCont.waitHandle = deferredObj;
            }).promise();
        }

        function doRender() {
            var file = formatTemplatePath(tmplName);
            $.get(file, null, function (template) {

                beforeLoad(data);

                var tmpl = $.templates(template);
                var htmlString = tmpl.render(data);
                if (targetSelector) {
                    $(targetSelector).replaceWith(htmlString);
                }

                templatesToLoadCount--;
                waitHandleCont.waitHandle.resolve();
                tryTriggerOnLoad();
            });
        }
    }


    function tryTriggerOnLoad() {
        var toWait = $.map(templatesToWait, function (el) { return el.req; });

        $.when.apply($, toWait)
         .then(function () {
             // templatesToLoadCount is needed because we never know which template was the last. so we try to trigger on load on every loaded template.
             if (templatesToLoadCount == 0 && !isLoaded) {
                 isLoaded = true;
                 $(document).ready(function () {
                     jQuery.extend($, jQuery);
                     _doLoad();
                 });
             }
         });
    }

    function formatTemplatePath(name) {
        return "/web/es/tmpl/_" + name + ".tmpl.html";
    }

    function registerOnLoad() {
        var jqueryBackup = window.$;
        if (typeof jqueryBackup == "undefined")
            throw "jQuery must be defined before register es-specific onload";

        jqueryBackup.extend($esload, jqueryBackup);

        window.$ = $esload;
        _doLoad = load;
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
