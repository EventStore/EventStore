define(['es-ui'], function (app) {

	'use strict';

	return app.config(['$httpProvider', function ($httpProvider) {
    
        // configuring httpProvider:

        //      all request will be json type
        $httpProvider.defaults.headers.common['Accept'] = 'application/json';
        //      all get request will use long polling
        //      get as object, as potst, put and common are only created in angularjs
        $httpProvider.defaults.headers.get = {
            'ES-LongPoll': 5 // in sec
        };

    }])

});