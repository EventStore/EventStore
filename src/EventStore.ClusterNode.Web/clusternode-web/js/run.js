define(['es-ui'], function (app) {

	'use strict';

	return app.run([
        '$rootScope', '$state', '$stateParams', 'AuthService',
        function ($rootScope, $state, $stateParams, authService) {

			// for testing purpose
            authService.existsAndValid()
            .then(function () {

            }, function () {
                $rootScope.$currentState = $state.current;
                $state.go('signin');
            })

            $rootScope.$state = $state;
            $rootScope.$stateParams = $stateParams;
        }
    ]);

});