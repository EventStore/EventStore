/*jshint sub: true,bitwise: false*/
define(['./_module'], function (app) {

    'use strict';

    // todo: remove State Params if we will not add query for existing queries

    return app.controller('QueryCtrl', [
		'$scope', '$state', '$stateParams', 'QueryService', 'ProjectionsMonitor', 'MessageService',
		function ($scope, $state, $stateParams, queryService, monitor, msg) {

			var location;

			function create () {
				var param = {
					emit: 'no',
					checkpoints: 'no',
					enabled: 'no'
				};

				queryService.create($scope.query, param)
				.success(function (data, status, headers) {
					location = headers()['location'];
					$scope.isCreated = true;
					run();
				})
				.error(function () {
					$scope.isCreated = false;
					msg.error('Couldn\'t create new query');
				});
			}


			function monitorState () {

				monitor.stop();
				monitor.start(location, {
					ignoreQuery: true,
					ignoreResult: true
				}).then(null, null, function (data) {
					var s;

					$scope.state = data.state;
					if(data.statistics && data.statistics.projections.length) {
						s = data.statistics.projections[0].status;
						$scope.status = s;

						$scope.isStopped = !!~s.indexOf('Stopped') || !!~s.indexOf('Faulted');
						// todo: shell we stop monitoring when status is completed/faulted?
					}
				});
			}

			function run () {
				var updated = queryService.update(location, $scope.query);
				
				updated.success(function () {
					var enabled = queryService.enable(location);
					// start monitoring ms before query will be enabled
					monitorState();

					enabled.error(function () {
						msg.error('Could not start query');
						monitor.stop();
					});
				})
				.error(function () {
					msg.error('Query not updated');
				});
			}

			$scope.aceConfig = {
				mode: 'javascript',
				useWrapMode: false,
				showGutter: true,
				theme: 'monokai'
			};

			$scope.disableStop = function () {

				if(!$scope.isCreated) {
					return true;
				}

				if($scope.isStopped) {
					return true;
				}

				return false;
			};

			$scope.run = function () {
				if($scope.isCreated) {
					run();
				} else {
					create();
				}
			};

			$scope.stop = function () {
				monitor.stop();

				queryService.disable(location)
				.error(function () {
					msg.error('Could not break query');
				});
			};

			$scope.debug = function () {
				$state.go('projections.item.debug', { 
					location: encodeURIComponent(location) 
				}, { 
					inherit: false 
				});
			};

			$scope.$on('$destroy', function () {
				monitor.stop();
			});
		}
	]);
});