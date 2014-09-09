define(['./_module'], function (app) {

	'use strict';

	return app.provider('AdminService', function () {
		this.$get = [
			'$http', 'urls', 'UrlBuilder',
			function ($http, urls, urlBuilder) {

				return {
					halt: function () {
						var url = urlBuilder.build(urls.admin.halt);
						return $http.post(url);
					},
					shutdown: function () {
						var url = urlBuilder.build(urls.admin.shutdown);
						return $http.post(url);
					},
					scavenge: function () {
						var url = urlBuilder.build(urls.admin.scavenge);
						return $http.post(url);
					}
				};
		}];
	});

});