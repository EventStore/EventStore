define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersItemDeleteCtrl', [
		'$scope', '$state', '$stateParams', 'UserService', 'MessageService',
		function ($scope, $state, $stateParams, userService, msg) {
			
			$scope.disable = true;
			$scope.confirm = function ($event) {
				$event.preventDefault();
				$event.stopPropagation();

				userService.remove($scope.user.loginName).then(function () {
					msg.info('user deleted');
					$state.go('users.list');
				}, function () {
					msg.error('user not deleted');
				});
			};

			userService.get($stateParams.username)
			.success(function (data) {
				$scope.user = data;
				$scope.disable = false;
			})
			.error(function () {
				msg.error('user does not exists or you do not have perms');
				$state.go('users');
			});
		}
	]);
});