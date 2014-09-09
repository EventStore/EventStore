define(['./_module'], function (app) {

	'use strict';

	return app.provider('QueryService', function () {
		this.$get = [
			'$http', '$q', 'urls', 'UrlBuilder', 'uri',

			function ($http, $q, urls, urlBuilder, uriProvider) {

				return {
					create: function (source, params) {
						var qp = uriProvider.getQuery(params),
							url = urlBuilder.build(urls.query.create) + qp;

						return $http.post(url, source);
					},
					update: function (url, source) {
						url = urlBuilder.simpleBuild(urls.query.update, url);
						return $http.put(url, source);
					},
					state: function (url) {
						url = urlBuilder.simpleBuild(urls.query.state, url);

						return $http.get(url);
					},
					enable: function (url) {
						url = urlBuilder.simpleBuild(urls.query.commands.enable, 
							url);

						return $http.post(url);
					},
					disable: function (url) {
						url = urlBuilder.simpleBuild(urls.query.commands.disable, 
							url);

						return $http.post(url);
					},
				};
			}
		];
	});

});