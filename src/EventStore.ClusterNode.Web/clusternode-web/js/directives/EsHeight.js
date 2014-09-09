define(['./_module'], function (app) {

    'use strict';

    return app.directive('esHeight', [function () {
		return {
			restrict: 'A',
	        link: function(scope, elm, attrs, ctrl) {

	        	elm.parent().addClass('login-height');
	        }
		};
	}]);


});