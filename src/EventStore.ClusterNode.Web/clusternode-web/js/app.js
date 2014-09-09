define([
	'angular',
	'uiRouter',
	'uiUtils',
	'uiAce',
	'./modules/projections/module',
	'./modules/admin/module',
	'./modules/dashboard/module',
	'./modules/streams/module',
	'./modules/security/module',
	'./modules/users/module',
	'./services/_index',
	'./directives/_index',
	'./templates/_index'
], function (ng) {
	'use strict';

	// defines application, and depedencies

	return ng.module('es-ui', [
		'es-ui.projections',
		'es-ui.admin',
		'es-ui.dashboard',
		'es-ui.streams',
		'es-ui.users',
		'es-ui.security',
		'es-ui.directives',
		'es-ui.services',
		'es-ui.templates',
		'ui.router',
		'ui.utils',
		'ui.ace'
	]);
});