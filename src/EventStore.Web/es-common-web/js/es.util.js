if (!window.es) { window.es = {}; };
if (es.util)
    throw 'es.util is already declared.';

es.util = {};

es.util.loadMenu = function (pages) {

    var tmplStr = '<li class="{{>className}}"> <a href="{{>link}}"> {{>name}} </a> </li>';
    var tmpl = $.templates(tmplStr);
    var htmlString = tmpl.render(pages);

    $("#navmenu").html(htmlString);

    tryHighlightActivePageLink();

    function tryHighlightActivePageLink() {
        var urlParts = window.location.href.split("/");
        var pageName = urlParts[urlParts.length - 1];
        var active = $("#navmenu li a[href='" + pageName + "']");
        if (active.length == 1)
            active.parent().addClass("active");
    }
};

es.util.formatError = function (text, xhr) {
    var reason = xhr.responseText
                     || (xhr.status === 0 ? "Couldn't connect to server." : null)
                     || xhr.statusText
                     || '(unknown)';
    return [text, "\r\nReason: ", reason].join('');
};