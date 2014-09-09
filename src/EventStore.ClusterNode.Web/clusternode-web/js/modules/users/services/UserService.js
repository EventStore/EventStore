define(['./_module'], function (app) {

	'use strict';

	return app.provider('UserService', function () {

		this.$get = [
			'$http', 'urls', 'UrlBuilder',
			function ($http, urls, urlBuilder) {

				return {
					all: function () {
						var url = urlBuilder.build(urls.users.list);

						return $http.get(url);
					},
					get: function (username) {
						var url = urlBuilder.build(urls.users.get, username);

						return $http.get(url);
					},
					create: function (user) {
						var url = urlBuilder.build(urls.users.create);

						user.groups = [];
						if (user.isAdmin) {
							user.groups.push('$admin'); // todo: move it somewhere
							delete user.isAdmin;
						}
						delete user.confirmPassword;

						return $http.post(url, user);
					},
					update: function (username, fullName, isAdmin) {
						var url = urlBuilder.build(urls.users.update, username),
							groups = [];

						if (isAdmin) {
							groups.push('$admin'); // todo: move it somewhere
						}

						return $http.put(url, { fullName: fullName, groups: groups});
					},
					remove: function (username) {
						var url = urlBuilder.build(urls.users.remove, username);

						return $http.delete(url);
					},
					disable: function (username) {
						var url = urlBuilder.build(urls.users.disable, username);

						return $http.post(url);
					},
					enable: function (username) {
						var url = urlBuilder.build(urls.users.enable, username);

						return $http.post(url);
					},
					resetPassword: function (username, data) {
						var url = urlBuilder.build(urls.users.resetPassword, username);

						return $http.post(url, {newPassword: data});
					}
				};
			}
		];
    });

});