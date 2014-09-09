define(['./_module'], function (app) {

	'use strict';

	return app.provider('ProjectionsService', function () {
		this.$get = [
			'$http', '$q', 'urls', 'UrlBuilder', 'uri',

			function ($http, $q, urls, urlBuilder, uriProvider) {

				var errors = '';
				function formatMultipleErrors (err, name) {
					errors += name + ': ' + err;
					errors += '\n\r';
				}

				function clearErrors () {
					errors = '';
				}

				function executeCommand (forItems, command) {
					var all = forItems(),
						deferred = $q.defer();
					clearErrors();
					all.success(function (data) {
						var calls = [], url;

						angular.forEach(data.projections, function (value) {
							url = value.statusUrl + command;
							calls.push($http.post(url).error(function (err) {
								formatMultipleErrors(err, value.name);
							}));
						});

						$q.allSettled(calls).then(function (values) {

							deferred.resolve(values);

						}, function () {
							deferred.reject(errors);
						});
					});

					all.error(function () {
						deferred.reject('Could\'t get projections list');
					});

					return deferred.promise;
				}

				return {
					all: function (withQueries) {
						var url;

						if(withQueries) {
							url = urlBuilder.build(urls.projections.any);
						} else {
							url = urlBuilder.build(urls.projections.allNonTransient);
						}

						return $http.get(url);
					},
					disableAll: function () {
						return executeCommand(this.all, urls.projections.disable);
					},
					enableAll: function () {
						return executeCommand(this.all, urls.projections.enable);
					},
					create: function (mode, source, params) {
						var qp = uriProvider.getQuery(params),
							url = urlBuilder.build(urls.projections.create, mode) + qp;

						return $http.post(url, source);
					},
					createStandard: function (name, type, source) {
						var url = urlBuilder.build(urls.projections.createStandard, name, type);

						return $http.post(url, source);
					},
					status: function (url) {
						url = urlBuilder.simpleBuild('%s', url);

						return $http.get(url);
					},
					state: function (url, params) {
						var qp;

						if(params) {
							qp = uriProvider.getQuery(params);
							url = urlBuilder.simpleBuild(urls.projections.state, url) + '?' + qp;
						} else {
							url = urlBuilder.simpleBuild(urls.projections.state, url);
						}

						return $http.get(url);
					},
					result: function (url) {
						url = urlBuilder.simpleBuild(urls.projections.result, url);

						return $http.get(url);
					},
					statistics: function (url) {
						url = urlBuilder.simpleBuild(urls.projections.statistics, url);

						return $http.get(url);
					},
					query: function (url, withoutConfig) {
						if(withoutConfig) {
							url = urlBuilder.simpleBuild(urls.projections.queryWithoutConfig, url);
						} else {
							url = urlBuilder.simpleBuild(urls.projections.query, url);
						}

						return $http.get(url);
					},
					readEvents: function(definition, position, count) {
						var params,
							url = urlBuilder.build(urls.projections.readEvents);

						count = count || 10;

						params = {
							query: definition,
							position: position,
							maxEvents: count
						};

						return $http.post(url, JSON.stringify(params));

					},
					remove: function (url, params) {
						var qp = uriProvider.getQuery(params);

						url = urlBuilder.simpleBuild(urls.projections.remove, url) + qp;

						return $http.delete(url);
					},
					reset: function (url) {
						url = urlBuilder.simpleBuild(urls.projections.commands.reset, 
							url);

						return $http.post(url);
					},
					enable: function (url) {
						url = urlBuilder.simpleBuild(urls.projections.commands.enable, 
							url);

						return $http.post(url);
					},
					disable: function (url) {
						url = urlBuilder.simpleBuild(urls.projections.commands.disable, 
							url);

						return $http.post(url);
					},
					updateQuery: function (url, emit, source) {

						if(source) {
							url = urlBuilder.simpleBuild(urls.projections.updateQuery, url) + emit;
						} else {
							source = emit;
							url = urlBuilder.simpleBuild(urls.projections.updatePlainQuery, url);
						}

						return $http.put(url, source);
						// 	, {
						// 	headers: {
						// 		'Content-Type': 'application/x-www-form-urlencoded'
						// 	}
						// });
					}
				};
		}];
	});

});