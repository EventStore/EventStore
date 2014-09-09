define(['./_module'], function (app) {

    'use strict';

    return app.directive('esQueueRow', [function () {
		return {
			restrict: 'A',
			templateUrl: 'dashboard.row.tpl.html',
			scope: {
				esQueue: '='
			},
			link: function () {
			}
		};
	}]);


});