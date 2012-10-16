if (!window.es) { window.es = {}; };
es.tmpl = (function () {

    return {
        renderHead: renderHead,
        renderBody: renderBody
    };

    function renderHead() {
        renderTemplate("head", "head", null);
    }

    function renderBody(contentSelector, scripts) {
        var $content = $(contentSelector);
        var content = $content.html();
        $content.remove();
        var data = {
            content: content,
            scripts: scripts || []
        };
        renderTemplate("body", "body", data);
    }

    function renderTemplate(tmplName, targetSelector, data) {
        var file = formatTemplatePath(tmplName);
        $.get(file, null, function (template) {
            var tmpl = $.templates(template);
            var htmlString = tmpl.render(data);
            if (targetSelector) {
                $(targetSelector).html(htmlString);
            }
            return htmlString;
        });
    }

    function formatTemplatePath(name) {
        return "tmpl/_" + name + ".tmpl.html";
    }
})();
