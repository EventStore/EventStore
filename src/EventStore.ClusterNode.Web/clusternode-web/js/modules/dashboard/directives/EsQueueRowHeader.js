define(['./_module'], function (app) {

    'use strict';

    return app.directive('esQueueRowHeader', [function () {
		return {
			restrict: 'A',
			templateUrl: 'dashboard.row.header.tpl.html',
			scope: {
				esQueue: '='
			},
			link: function (scope, elem) {

				scope.toggle = function () {
					scope.esQueue.show = !scope.esQueue.show;
				};

				// workaround for replace issuse: https://github.com/angular/angular.js/issues/1459
				elem.bind('click', function () {
					scope.$apply(function () {
						scope.toggle();
					});
				});
			}
		};
	}]);


});