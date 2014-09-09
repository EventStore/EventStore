define(['./_module'], function (app) {

    'use strict';

    return app.controller('SignInCtrl', [
		'$scope', '$rootScope', '$state', 'AuthService', 'MessageService',
		function ($scope, $rootScope, $state, authService, msg) {

			$scope.log = {
				username: '',
				password: '',
				server: ''
			};
			$scope.signIn = function () {
				if ($scope.login.$invalid) {
					msg.warn('Please fix all validation errors');
					return;
				}

				authService.validate($scope.log.username, $scope.log.password, $scope.log.server)
				.success(function () {
					authService.setCredentials($scope.log.username, $scope.log.password, $scope.log.server);
					redirectToPreviousState();
				})
				.error(function () {
					msg.warn('Server does not exists or wrong user data');
				});
			};


			function redirectToPreviousState () {
				if($rootScope.currentState) {
					$state.go($rootScope.currentState)
				} else {
					$state.go('dashboard.list');
				}
			}

			function checkCookie () {
				
				authService.existsAndValid()
				.then(function () {
					redirectToPreviousState();
				});
			}

			checkCookie();

		}
	]);


});