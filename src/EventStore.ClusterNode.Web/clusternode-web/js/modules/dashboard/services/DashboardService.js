define(['./_module'], function (app) {

	'use strict';

	return app.provider('DashboardService', function () {

		this.$get = [
			'$http', 'urls', 'UrlBuilder',
			function ($http, urls, urlBuilder) {

				return {
					stats: function () {
						var url = urlBuilder.build(urls.stats);

						return $http.get(url);
					}
				};
			}
		];
    });

});