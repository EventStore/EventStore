define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersItemEnableCtrl', [
		'$scope', '$state', '$stateParams', 'UserService', 'MessageService',
		function ($scope, $state, $stateParams, userService, msg) {
			
			$scope.disable = true;
			$scope.confirm = function ($event) {
				$event.preventDefault();
				$event.stopPropagation();

				userService.enable($scope.user.loginName)
				.success(function () {
					msg.info('user enabled');
					$state.go('^.details');
				})
				.error(function () {
					msg.error('user not enabled');
				});
			};

			userService.get($stateParams.username)
			.success(function (data) {
				$scope.user = data.data;
				$scope.disable = false;

				if(!$scope.user.disabled) {
					msg.warn('user already enabled');
					$state.go('^.details');
				}
			})
			.error(function () {
				msg.error('user does not exists or you do not have perms');
				$state.go('users');
			});
		}
	]);
});