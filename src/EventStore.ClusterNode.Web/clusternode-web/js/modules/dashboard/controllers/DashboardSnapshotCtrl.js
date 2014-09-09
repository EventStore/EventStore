define(['./_module'], function (app) {

    'use strict';

    return app.controller('DashboardSnaphostCtrl', [
		'$scope', 'DashboardService', 'SprintfService',
		function ($scope, dashboardService, print) {

			function format (data) {
				var snapshot = 'Snapshot taken at: ' + (new Date()).toString();
				
				snapshot += '\n\r';
				snapshot += print.format('%-30s  %15s  %15s  %15s  %9s  %15s  %45s', 'Name', 'Length', 'Rate (items/s)', 'Time (ms/item)', 'Activity', 'Item Processed', 'Current/Last message');
				snapshot += '\n\r';
				snapshot += print.format('%-30s  %8s  %5s', '', 'Current', 'Peak');
				snapshot += '\n\r';
				snapshot += print.format('%\'=30s  %\'=8s  %\'=5s  %\'=15s  %\'=15s  %\'=9s  %\'=15s  %\'=45s', '=', '=', '=', '=', '=', '=', '=', '=');
				
				angular.forEach(data.es.queue, function (item) {
					snapshot += '\n\r';
					snapshot += print.format('%-30s  %8s  %5s  %15s  %15s  %9s  %15s  %45s', 
						item.queueName, 
						item.lengthCurrentTryPeak, 
						item.lengthLifetimePeak,
						item.avgItemsPerSecond,
						item.avgProcessingTime.toFixed(3),
						item.busy,
						item.totalItemsProcessed,
						item.inProgressMessage + ' / ' + item.lastProcessedMessage
					);
				});

				$scope.snapshot = snapshot;
			}
			dashboardService.stats().success(format);
		}
	]);
});