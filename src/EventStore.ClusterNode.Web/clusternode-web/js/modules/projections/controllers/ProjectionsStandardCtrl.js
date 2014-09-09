/*jshint sub: true*/
define(['./_module'], function (app) {

    'use strict';

    return app.controller('ProjectionsStandardCtrl', [
		'$scope', '$state', 'ProjectionsService', 'MessageService',
		function ($scope, $state, projectionsService, msg) {

			$scope.types = [{
				value: 'native:EventStore.Projections.Core.Standard.IndexStreams',
				name: 'Index Streams'
			}, {
				value: 'native:EventStore.Projections.Core.Standard.CategorizeStreamByPath',
				name: 'Categorize Stream by Path'
			}, {
				value: 'native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath',
				name: 'Categorize Event by Stream Path'
			}, {
				value: 'native:EventStore.Projections.Core.Standard.IndexEventsByEventType',
				name: 'Index Events by Event Type'
			}, {
				value: 'native:EventStore.Projections.Core.Standard.StubHandler',
				name: 'Reading Speed Test Handler'
			}];

			$scope.aceConfig = {
				mode: 'javascript',
				useWrapMode: false,
				showGutter: true,
				theme: 'monokai'
			};
			
			$scope.type = 'native:EventStore.Projections.Core.Standard.IndexStreams';
			$scope.save = function () {

				if($scope.newProj.$invalid) {
					msg.info('please fix all validation errors');
					return;
				}

				projectionsService.createStandard($scope.name, $scope.type, $scope.source)
					.success(function (data, status, headers) {
						var location = headers()['location'];
						$state.go('^.item.details', {
							location: encodeURIComponent(location)
						});
					})
					.error(function () {
						msg.error('Coudn\'t create new standard projection');
					});
			};
		}
	]);
});