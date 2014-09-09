define(['./_module'], function (app) {

    'use strict';

    return app.controller('ProjectionsItemDeleteCtrl', [
		'$scope', '$state', '$stateParams', 'ProjectionsService', 'MessageService',
		function ($scope, $state, $stateParams, projectionsService, msg) {

			$scope.location = $stateParams.location;

			$scope.projection = {
				name: '',
				source: '',
				state: ''
			};

			projectionsService.state($scope.location)
			.success(function (data) {
				$scope.projection.state = data;
			});

			projectionsService.query($scope.location)
			.success(function (data) {
				$scope.projection.source = data.query;
				$scope.projection.name = data.name;
			});

			$scope.remove = function ($event) {
				$event.preventDefault();
				$event.stopPropagation();

				projectionsService.remove($scope.location, {
					deleteCheckpointStream: $scope.deleteCheckpoint ? 'yes' : 'no',
					deleteStateStream: $scope.deleteState ? 'yes' : 'no'
				})
				.success(function () {
					msg.info('Projection removed');
					$state.go('projections.list');
				})
				.error(function () {
					msg.error('Projection not removed');
				});
			};
		}
	]);
});