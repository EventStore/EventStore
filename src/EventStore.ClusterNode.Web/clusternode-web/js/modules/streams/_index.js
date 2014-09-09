define([
	'angular',
	'uiRouter',
    './controllers/_index',
    './directives/_index',
    './services/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.streams', [
		'ui.router',
    	'es-ui.streams.controllers',
    	'es-ui.streams.services',
        'es-ui.streams.templates',
        'es-ui.streams.directives'
	]);
});