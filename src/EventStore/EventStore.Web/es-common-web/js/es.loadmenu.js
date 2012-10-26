if (!window.es) { window.es = {}; };
if (!es.utils) { es.utils = {}; };

es.utils.loadMenu = function(pages) {
  
    var tmplStr = '<li class="{{>class}}"> <a href="{{>link}}"> {{>name}} </a> </li>';
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