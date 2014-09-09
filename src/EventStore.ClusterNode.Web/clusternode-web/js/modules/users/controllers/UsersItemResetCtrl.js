define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersItemResetCtrl', [
		'$scope', '$state', 'UserService', 'MessageService',
		function ($scope, $state, userService, msg) {
			
			$scope.confirm = function () {
				if ($scope.resetPwd.$invalid) {
					msg.warn('Please fix all validation errors');
					return;
				}

				userService.resetPassword($scope.user.loginName, $scope.password)
				.success(function () {
					msg.info('password reseted');
					$state.go('^.details');
				})
				.error(function () {
					msg.error('password not reseted');
				});
			};

			userService.get($scope.$stateParams.username)
			.success(function (data) {
				$scope.user = data.data;
			})
			.error(function () {
				msg.error('user does not exists or you do not have perms');
				$state.go('users');
			});
		}
	]);
});