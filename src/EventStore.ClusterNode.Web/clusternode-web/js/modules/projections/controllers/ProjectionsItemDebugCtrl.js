define(['./_module'], function (app) {

    'use strict';

    return app.controller('ProjectionsItemDebugCtrl', [
		'$scope','$state', '$stateParams','$q', '$timeout', 'ProjectionsService', 'MessageService',
		function ($scope, $state, $stateParams, $q, $timeout, projectionsService, msg) {

			function updateStatusInfo (message) {
				if(!message) {
					message = 'N/A';
				}
				$scope.statusInfo = message;
			}

			function setPartition (partition) {
				partition = '(' + partition + ')';
				$scope.currentPartition = partition;
			}

			updateStatusInfo('');
			$scope.location = $stateParams.location;

			$scope.aceConfig = {
				mode: 'javascript',
				useWrapMode: false,
				showGutter: true,
				theme: 'monokai'
			};

			$scope.aceEventsConfig = {
				mode: 'json',
				useWrapMode: false,
				showGutter: true,
				theme: 'monokai'
			};

			$scope.isRunning = false;
			$scope.query = '';
			$scope.events = '';
			$scope.state = '';
			
			var rawEvents, currentEvent, definition, currentPosition, partition, initialized, processor, cachedStates = {};

			var requests = [];
			updateStatusInfo('Loading definition...');
			var stats = projectionsService.statistics($scope.location);
			var state = projectionsService.state($scope.location);
			var query = projectionsService.query($scope.location);

			requests.push(stats);
			requests.push(state);
			requests.push(query);

			$q.allSettled(requests)
			.then(function (data) {
				// 0 - stats
				// 1 - state
				// 2 - query
				var stats = data[0],
					state = data[1],
					query = data[2],
					position;

				if(stats.data.status !== 'Stopped' && stats.data.status !== 'Faulted') {
					//msg.warn('Projection needs to be stopped before it can be debugged');
					$scope.isRunning = true;
				} 

				position = state.headers()['es-position'];
				currentPosition = angular.fromJson(position); 
				definition = query.data.definition;
				
				$scope.query = query.data.query;

				updateStatusInfo('');
				loadEvents();
			});

			function loadEvents () {
				projectionsService.readEvents(definition, currentPosition)
				.success(function (data) {
					$scope.events = JSON.stringify(data, undefined, 4);
					rawEvents = data.events;

					if(rawEvents && rawEvents.length) {
						prepare ();
					} else {
						updateStatusInfo('No further events are available. Waiting...');
						$timeout(loadEvents, 1000);
					}

				})
				.error(function () {
					$timeout(loadEvents, 1000);
				});
			}

			function prepare () {
				currentEvent = rawEvents[0];

				if(initialized) {
					loadState();
					return;
				}

				updateStatusInfo('Running the definition...');
				$scope.$broadcast('load-scripts');
				$timeout(loadState, 100);

			}

			function loadState () {

				processor = document.getElementById('script-placeholder').contentWindow.processor;
				if(!processor) {
					$timeout(loadState, 1000);
					return;
				}

				initialized = true;
				updateStatusInfo('');
				partition = null;

				if(definition.byCustomPartitions) {
					partition = processor.get_state_partition(
                            currentEvent.isJson ? angular.toJson(currentEvent.data) : currentEvent.data,
                            currentEvent.eventStreamId,
                            currentEvent.eventType,
                            currentEvent.category,
                            currentEvent.eventNumber,
                            currentEvent.isJson ? angular.toJson(currentEvent.metadata) : currentEvent.metadata);
				} else if(definition.byStream) {
					partition = currentEvent.eventStreamId;
				} else {
					partition = '';
				}

				setPartition(partition);

				if(cachedStates[partition]) {
					stateLoaded(cachedStates[partition]);
				} else {
					updateStatusInfo('Loading the projection state...');

					projectionsService.state($scope.location, {
						partition: partition
					})
					.success(stateLoaded)
					.error(function () {
						updateStatusInfo('Error loading the projection state');
					});
				}
			}

			function stateLoaded (data) {
				var cached;
				updateStatusInfo('Ready for debugging!');
				$scope.isRunning = false;
				if(!data) {
					processor.initialize();
					cachedStates[partition] = processor.debugging_get_state();
				} else {
					processor.set_state(data);
				}
				try {
					cached = angular.fromJson(cachedStates[partition]);
					$scope.state = cached;
				} catch (e) {
					$scope.state = '';
				}
			}

			$scope.runStep = function () {
				var state = processor.process_event(
                    currentEvent.isJson ? JSON.stringify(currentEvent.data) : currentEvent.data,
                    currentEvent.isJson,
                    currentEvent.eventStreamId,
                    currentEvent.eventType,
                    currentEvent.category,
                    currentEvent.eventNumber,
                    currentEvent.isJson ? JSON.stringify(currentEvent.metadata) : currentEvent.metadata,
                    partition);


                cachedStates[partition] = state;
                //console.log(currentEvent.readerPosition);
                currentPosition = currentEvent.readerPosition;
                //eventsLoaded = false;
                loadEvents();
			};

			$scope.update = function () {
				projectionsService.updateQuery($scope.location, $scope.query)
				.success(function () {
					msg.info('projection updated');
					// todo: not sure, we can reset debugging state, or
					// transfer user to different page?

					//$state.transitionTo($state.current, $stateParams, { reload: true, inherit: false, notify: true });
					msg.info('there is a bug in UI-Router that does not reload controllers after $state.reload, please click F5');
					$state.reload();
				})
				.error(function () {
					msg.error('Projection not updated');
				});
			};
			$scope.stop = function () {
				projectionsService.disable($scope.location)
				.success(function () {
					msg.info('projection stopped');
					$scope.isRunning = false;
				})
				.error(function () {
					msg.error('projection could not be stopped');
				});
			};
		}
	]);
});