(function () {

    var mainMenu = [
        { "name": "Home", "link": "/web/home.htm", "class": "" },
        { "name": "Streams", "link": "/web/streams.htm", "class": "" }
    ];

    var mainMenuLow = [
        { "name": "Charts", "link": "/web/charts.htm", "class": "" },
        { "name": "Health Charts", "link": "/web/health-charts.htm", "class": "" },
        { "name": "Queues", "link": "/web/queues.htm", "class": "" }
    ];
    
    var webUrl = "";
    
    $(function () {

        $.templates("editSourceTemplate", "#editSourceTemplate");
        webUrl = location.origin;
        console.log(webUrl);

        $.ajax(webUrl + "/web/es/js/projections/resources/es.menu.part.js", {
            headers: {
                Accept: "application/json",
            },

            type: "GET",
            data: $("#source").val(),
            success: successUpdateSource,
            error: onError
        });
    });
    
    function successUpdateSource(data, status, xhr) {
        var menu;
        debugger;
        if (data != null) {
            dataObj = JSON.parse(data);
            menu = mainMenu.concat(dataObj);
        }
        else {
            menu = mainMenu.concat([]);
        }
        
        menu = menu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }
    
    function onError(xhr) {
        debugger;
        
        var msg = es.util.formatError("Couldn't load projections menu", xhr);
        console.log(msg);
        
        var menu = mainMenu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }

})();