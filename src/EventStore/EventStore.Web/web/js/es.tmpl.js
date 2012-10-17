if (!window.es) { window.es = {}; };
es.tmpl = (function () {

    return {
        renderHead: renderHead,
        renderBody: renderBody
    };

    function renderHead() {
        renderTemplate("head", "#r-head", null);
    }

    function renderBody() {
        var $content = $("#content");
        var content = $content.html();
        $content.remove();
        var data = {
            content: content
        };
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
})();
