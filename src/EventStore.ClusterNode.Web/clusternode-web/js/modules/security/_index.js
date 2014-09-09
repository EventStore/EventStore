define([
	'angular',
	'uiRouter',
    './controllers/_index',
    //'./services/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.security', [
		'ui.router',
    	'es-ui.security.controllers',
    	//'es-ui.security.services',
        'es-ui.security.templates'
	]);
});