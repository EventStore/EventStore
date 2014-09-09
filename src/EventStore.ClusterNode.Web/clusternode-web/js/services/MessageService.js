define(['./_module'], function (app) {

	'use strict';

	return app.factory('MessageService', [
		function () {

			return {
				info: function (text) {
					alert(text);
				},
				warn: function (text) {
					alert(text);
				},
				error: function (text) {
					alert(text);
				},
				confirm: function (text) {
					return confirm(text);
				}
			};
		}
	]);

});