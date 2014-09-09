define(['./_module', 'sprintf'], function (app, sprintf) {

	'use strict';

	return app.factory('SprintfService', [
		function () {

			return {
				format: function () {
					var args = [].slice.call(arguments);

					return sprintf.apply(null, args);
				}
			};
		}
    ]);
});