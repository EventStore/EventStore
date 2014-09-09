define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersItemDetailsCtrl', [
		'$scope', '$state', '$stateParams', 'UserService',
		function ($scope, $state, $stateParams, userService) {
			
			userService.get($stateParams.username)
			.success(function (data) {
				$scope.user = data.data;
			})
			.error(function () { 
				$state.go('users');
			});

		}
	]);
});