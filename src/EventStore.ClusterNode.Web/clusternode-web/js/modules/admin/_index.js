define([
	'angular',
	'uiRouter',
    './controllers/_index',
    './services/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.admin', [
		'ui.router',
    	'es-ui.admin.controllers',
    	'es-ui.admin.services',
        'es-ui.admin.templates'
	]);
});