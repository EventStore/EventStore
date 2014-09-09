/*jshint bitwise: false*/
define(['./_module'], function (app) {

    'use strict';

    return app.controller('ProjectionsItemDetailsCtrl', [
		'$scope', '$state', '$stateParams', 'ProjectionsService', 'ProjectionsMonitor', 'MessageService',
		function ($scope, $state, $stateParams, projectionsService, monitor, msg) {

			var lastSinceRestart = null,
				lastTimestamp = null;

			$scope.location = $stateParams.location;

			$scope.aceConfig = {
				mode: 'javascript',
				useWrapMode: false,
				showGutter: true,
				theme: 'monokai'
				// onLoad: function (_editor) {
				// 	_editor.setReadOnly(true);
				// 	console.log(_editor);
				// }
			};

			function setButtons () {
				// todo: refactor, don't like indexOf and not sure if i do test proper things
				var s = $scope.stats.status;

				$scope.isRunning = !(s.indexOf('Loaded') === 0 ||
                        s.indexOf('Stopped') === 0 ||
                        s.indexOf('Completed') === 0 ||
                        s.indexOf('Faulted') === 0);

				$scope.isStopped = !!~s.indexOf('Stopped');
			}

			monitor.start($scope.location)
			.then(null, null, function (data) {
				if(data.statistics && data.statistics.projections.length) {
					$scope.stats = data.statistics.projections[0];

					if (lastSinceRestart !== null) {
                        $scope.stats.eventsPerSecond = (1000.0 * ($scope.stats.eventsProcessedAfterRestart - lastSinceRestart) / (new Date() - lastTimestamp)).toFixed(1);
                    }
                    lastTimestamp = new Date();
                    lastSinceRestart = $scope.stats.eventsProcessedAfterRestart;

					setButtons();
				}

				if(data.query) {
					$scope.stream = data.query.definition.resultStreamName;
					$scope.query = data.query.query;
				}

				if(data.result) {
					$scope.result = data.result;
				}

				if(data.state) {
					$scope.sate = data.state;
				}
			});

			$scope.reset = function () {
				var m = 'Projection reset is an unsafe operation. Any previously emitted events will be emitted again to the same streams and handled by their subscribers.\n\rAre you sure?',
					confirmation = msg.confirm(m);

				if(!confirmation) {
					return;
				}

				projectionsService.reset($scope.location)
				.success(function () {
					msg.info('projection reseted');
				})
				.error(function () {
					msg.error('reset failed');
				});
			};

			$scope.start = function () {
				projectionsService.enable($scope.location)
				.success(function () {
					msg.info('projection started');
				})
				.error(function () {
					msg.error('projection could not be started');
				});
			};

			$scope.stop = function () {
				projectionsService.disable($scope.location)
				.success(function () {
					msg.info('projection stopped');
				})
				.error(function () {
					msg.error('projection could not be stopped');
				});
			};

			$scope.$on('$destroy', function () {
				monitor.stop();
			});
		}
	]);
});