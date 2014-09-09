define([
	'angular',
	'uiRouter',
    './controllers/_index',
    './services/_index',
    './directives/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.dashboard', [
		'ui.router',
        'es-ui.dashboard.templates',
    	'es-ui.dashboard.controllers',
    	'es-ui.dashboard.services',
        'es-ui.dashboard.directives'
	]);
});