define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersNewCtrl', [
		'$scope', '$state', 'UserService', 'MessageService',
		function ($scope, $state, userService, msg) {

			$scope.newUser = {};
			$scope.confirm = function () {
				if ($scope.newUsr.$invalid) {
					msg.warn('Please fix all validation errors');
					return;
				}

				userService.create($scope.newUser)
				.success(function () {
					msg.info('user created');
					$state.go('^.list');
				})
				.error(function () {
					msg.error('user not created');
				});
			};
		}
	]);
});