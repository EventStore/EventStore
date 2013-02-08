(function () {

    var mainMenu = [
        { "name": "Home", "link": "/web/home.htm", "class": "" },
        { "name": "Streams", "link": "/web/streams.htm", "class": "" }
    ];
    
    var pendingRequestsCount = 0;
    var menuParts = [];

    var mainMenuLow = [
        { "name": "Charts", "link": "/web/charts.htm", "class": "" },
        { "name": "Health Charts", "link": "/web/health-charts.htm", "class": "" },
        { "name": "Queues", "link": "/web/queues.htm", "class": "" }
    ];

    var buildMenuMaxTryCount = 60;

    function buildMenu() {
        if (pendingRequestsCount != 0 && buildMenuMaxTryCount > 0) {
            buildMenuMaxTryCount -= 1;
            setTimeout(function () { buildMenu(); }, 25);
            return;
        }

        var menu = mainMenu.concat(menuParts);
        menu = menu.concat(mainMenuLow);
        es.util.loadMenu(menu);
    }
    
    $(function () {

        var webUrl = location.href.replace(location.pathname, "");
        
        pendingRequestsCount += 1;

        $.ajax(webUrl + "/sys/subsystems", {
            headers: {
                Accept: "application/json"
            },

            type: "GET",
            data: $("#source").val(),
            success: subsytemsListReceived,
            error: onErrorListSubsystems
        });
        
        buildMenu();
    });
    
    function subsytemsListReceived(data, status, xhr) {
        
        var subsystemsList,
            i,
            item,
            msg;

        if (!data) {
            pendingRequestsCount -= 1;
        }
        else {
            subsystemsList = data;

            for (i = 0; i < subsystemsList.length; i++) {
                item = subsystemsList[i];
                switch (item) {
                    case "Projections":
                        pendingRequestsCount += 1;
                        loadProjectionsMenu();
                        break;
                    default:
                        msg = "Not expected subsystem " + item + " has been found in list.";
                        console.log(msg);
                        break;
                }
            }
            pendingRequestsCount -= 1;
        }
    }
    
    function onErrorListSubsystems(xhr) {
        var msg = es.util.formatError("Couldn't load node subsystems list", xhr);
        console.log(msg);

        pendingRequestsCount -= 1;
    }
    
    function loadProjectionsMenu() {
        $(function () {

            webUrl = location.href.replace(location.pathname, "");

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
        
        pendingRequestsCount -= 1;
    }
    
    function onErrorProjectionsMenu(xhr) {
        var msg = es.util.formatError("Couldn't load projections menu", xhr);
        console.log(msg);
        
        pendingRequestsCount -= 1;
    }

})();