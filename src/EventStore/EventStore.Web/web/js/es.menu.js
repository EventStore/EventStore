$(function () {

    function renderMenu(data, status, xhr) {

        $.templates("navmenuTemplate", "#navmenuTemplate");

        $("#navmenu").html(
            $.render.navmenuTemplate(data.navmenuTemplate)
        );

        var methodname = window.location.href.split("/");
        methodname = methodname[methodname.length - 1];

        $("#navmenu li a[href='"+methodname+"']").parent().addClass("active");

    }

    function error(xhr, status) {
        menuList();
    }

    function menuList() {
        $.ajax({
            url: "menu_ajax",
            success: renderMenu,
            error: error
        });
    }


    //menuList();
    var data = { "navmenuTemplate" :
        [
            { "name": "Home", "link": "index.htm", "active": "" },
            { "name": "List Existing", "link": "list-projections.htm", "active": "" },
            { "name": "Post New", "link": "post-projection.htm", "active": "" },
            { "name": "Create A Standard Projection", "link": "post-standard-projection.htm", "active": "" },
            { "name": "Charts", "link": "dashboard.htm", "active": "" },
            { "name": "Health Charts", "link": "dashboard-health.htm", "active": "" },
            { "name": "Statistics", "link": "statistics.htm", "active": "" }
        ]
    };

    renderMenu(data);
});