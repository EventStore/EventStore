define(['./_module'], function (app) {

    'use strict';

    return app.controller('SignOutCtrl', [
		'$scope', '$state', 'AuthService',
		function ($scope, $state, authService) {
			authService.clearCredentials();
			$state.go('signin');
		}
	]);


});