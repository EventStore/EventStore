define(['./_module'], function (app) {

    'use strict';

    return app.controller('StreamsListCtrl', [
		'$scope', '$state', 'StreamsService',
		function ($scope, $state, streamsService) {
			
			function filter (entries) {
				var filtered = {}, i = 0, length = entries.length, item, result = [];

				for(; i<length; i++) {
					item = entries[i];
					filtered[item.streamId] = true;
				}

				for (item in filtered) {
					result.push({ streamId: item });
				}

				return result;
			}

			$scope.search = '$all';

			$scope.gotoStream = function ($event) {
				$event.preventDefault();
				$event.stopPropagation();

				// todo: do check if stream exists

				$state.go('^.item.events', { streamId: $scope.search });
			};

			streamsService.recentlyChangedStreams()
			.success(function (data) {
				$scope.changedStreams = filter(data.entries);
			});

			streamsService.recentlyCreatedStreams()
			.success(function (data) {
				$scope.createdStreams = filter(data.entries);
			});
		}
	]);
});

