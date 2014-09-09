define([
	'angular',
	'uiRouter',
    './controllers/_index',
    './services/_index',
    './directives/_index',
    './templates/templates'
], function (ng) {

	'use strict';
    return ng.module('es-ui.projections', [
		'ui.router',
    	'es-ui.projections.controllers',
    	'es-ui.projections.services',
    	'es-ui.projections.directives',
        'es-ui.projections.templates'	
	]);
});