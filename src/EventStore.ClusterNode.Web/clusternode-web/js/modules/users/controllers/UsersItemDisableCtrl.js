define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersItemDisableCtrl', [
		'$scope', '$state', '$stateParams', 'UserService', 'MessageService',
		function ($scope, $state, $stateParams, userService, msg) {
			
			$scope.disable = true;
			$scope.confirm = function ($event) {
				$event.preventDefault();
				$event.stopPropagation();

				userService.disable($scope.user.loginName)
				.success(function () {
					msg.info('user disabled');
					$scope.$state.go('^.details');
				})
				.error(function () {
					msg.error('user not disabled');
				});
			};

			userService.get($stateParams.username)
			.success(function (data) {
				$scope.user = data.data;
				$scope.disable = false;
				if($scope.user.disabled) {
					msg.warn('user already disabled');
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