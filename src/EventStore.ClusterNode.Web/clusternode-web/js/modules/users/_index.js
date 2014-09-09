define([
	'angular',
	'uiRouter',
    './controllers/_index',
    './services/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.users', [
		'ui.router',
    	'es-ui.users.controllers',
    	'es-ui.users.services',
        'es-ui.users.templates'
	]);
});