(function () {

    var mainMenu = [
        { "name": "Home", "link": "/web/home.htm", "className": "" },
        { "name": "Streams", "link": "/web/streams.htm", "className": "" }
    ];
    
    var pendingRequestsCount = 0;
    var menuParts = [];

    var mainMenuLow = [
        { "name": "Charts", "link": "/web/charts.htm", "className": "" },
        { "name": "Queues", "link": "/web/queues.htm", "className": "" },
        { "name": "Admin", "link": "/web/admin.htm", "className": "" },
        { "name": "Users", "link": "/web/users/users.htm", "className": "" },
        { "name": "MyAccount", "link": "/web/users/my_account.htm", "className": "" }
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
    
    function getBaseUrl() {
        return location.protocol + '//' + location.host;
    }
    
    
    $(function () {
        var webUrl = getBaseUrl();
        
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
            var webUrl = getBaseUrl();

            $.ajax(webUrl + "/web/es/js/projections/resources/es.menu.part.json", {
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
        if (data != null) {
            menuParts = menuParts.concat(data);
        }
        pendingRequestsCount -= 1;
    }
    
    function onErrorProjectionsMenu(xhr) {
        var msg = es.util.formatError("Couldn't load projections menu", xhr);
        console.log(msg);
        
        pendingRequestsCount -= 1;
    }

})();