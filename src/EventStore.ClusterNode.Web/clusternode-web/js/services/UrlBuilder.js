define(['./_module', 'angular'], function (app, angular) {

	'use strict';

	return app.factory('UrlBuilder', [
		'urls', '$rootScope', 'SprintfService',
		function (urls, $rootScope, print) {

			return {
				simpleBuild: function (format, url) {
					return print.format(format, decodeURIComponent(url));
				},
				build: function (url) {
					var args = [].slice.call(arguments, 1),
						params = [];

					// we want to encode uri components
					params.push(url);
					angular.forEach(args, function (value) {
						this.push(encodeURIComponent(value));
						//this.push(value);
					}, params);

					url = print.format.apply(null, params);

					return $rootScope.baseUrl + url;
				}
			};
		}
	]);

});