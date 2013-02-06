(function () {

    var mainMenu = [
        { "name": "Home", "link": "/web/home.htm", "class": "" },
        { "name": "Streams", "link": "/web/streams.htm", "class": "" }
    ];
    
    var expectedPartsNumber = 1;
    
    var partsNumber = expectedPartsNumber;
    var menuParts = [];

    var mainMenuLow = [
        { "name": "Charts", "link": "/web/charts.htm", "class": "" },
        { "name": "Health Charts", "link": "/web/health-charts.htm", "class": "" },
        { "name": "Queues", "link": "/web/queues.htm", "class": "" }
    ];

    var webUrl = "";
    
    $(function () {

        webUrl = location.origin;

        $.ajax(webUrl + "/sys/subsystems", {
            headers: {
                Accept: "application/json",
            },

            type: "GET",
            data: $("#source").val(),
            success: subsytemsListReceived,
            error: onErrorListSubsystems
        });
        
        buildMenu();
    });
    
    var buildMenuMaxTryCount = 60;
    
    function buildMenu() {
        if (partsNumber != 0 && buildMenuMaxTryCount > 0) {
            setTimeout(function () { buildMenu(); }, 25);
            buildMenuMaxTryCount -= 1;
            return;
        }
        
        var menu = mainMenu.concat(menuParts);
        menu = menu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }

    function subsytemsListReceived(data, status, xhr) {
        if (!data) {
            return;
        }
        
        var subsystemsList = data;

        for (var i = 0; i < subsystemsList.length; i++) {
            var item = subsystemsList[i];
            switch (item) {
                case "Projections":
                    partsNumber += 1;
                    loadProjectionsMenu();
                    break;
                default:
                    {
                        var msg = "Not expected subsystem " + item + " has been found in list.";
                        console.log(msg);
                    }
                    break;
            }
        }
        
        partsNumber -= expectedPartsNumber;
    }
    
    function loadProjectionsMenu() {
        $(function () {

            webUrl = location.origin;

            $.ajax(webUrl + "/web/es/js/projections/resources/es.menu.part.js", {
                headers: {
                    Accept: "application/json",
                },

                type: "GET",
                data: $("#source").val(),
                success: successUpdateSource,
                error: onErrorProjectionsMenu
            });
        });
    }
    
    function successUpdateSource(data, status, xhr) {
        var additionalMenu = data != null ? JSON.parse(data) : [];
        menuParts = menuParts.concat(additionalMenu);
        
        partsNumber -= 1;
    }
    
    function onErrorProjectionsMenu(xhr) {
        var msg = es.util.formatError("Couldn't load projections menu", xhr);
        console.log(msg);
        
        var menu = mainMenu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }
    
    function onErrorListSubsystems(xhr) {
        var msg = es.util.formatError("Couldn't load node subsystems list", xhr);
        console.log(msg);

        var menu = mainMenu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }

})();