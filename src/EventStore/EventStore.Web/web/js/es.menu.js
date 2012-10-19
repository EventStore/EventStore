(function () {
    var pages =
        [
            { "name": "Home", "link": "index.htm", "class": "" },
            { "name": "Projections", "link": "list-projections.htm", "class": "" },
            { "name": "New Projection", "link": "post-projection.htm", "class": "" },
            { "name": "Charts", "link": "dashboard.htm", "class": "" },
            { "name": "Health Charts", "link": "dashboard-health.htm", "class": "" },
            { "name": "Queues", "link": "statistics.htm", "class": "" }
        ];

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
})();