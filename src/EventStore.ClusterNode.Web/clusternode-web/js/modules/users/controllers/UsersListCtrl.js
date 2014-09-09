define(['./_module'], function (app) {

    'use strict';

    return app.controller('UsersListCtrl', [
		'$scope', 'UserService', 'poller', 'MessageService',
		function ($scope, userService, poller, msg) {

			var all = poller.create({
				intevral: 1000,
				action: userService.all,
				params: [
				]
			});

			all.start();
			all.promise.then(null, null, function (data) {
				$scope.users = data.data;
			});

			all.promise.catch(function () {
				all.stop();
				msg.error('Cannot get list of users');
			});


			$scope.$on('$destroy', function () {
				poller.clear();
			});
		}
	]);
});