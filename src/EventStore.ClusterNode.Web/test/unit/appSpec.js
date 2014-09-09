/*jshint bitwise: false*/
define([
	'angular',
	'angularMocks',
	'app'
], function(ng, mocks, app) {
	'use strict';

	describe('ES-UI: Modules', function () {
		describe('ES-UI Module:', function () {

			var module;
			beforeEach(function () {
				module = ng.module('es-ui');
			});

			it('should be registered', function () {
				expect(module).not.toBeNull();
			});

			describe('Depedencies:', function () {

				var deps,
					hasModule = function (m) {
					return !!~deps.indexOf(m);
				};

				beforeEach(function () {
					deps = module.value('appName').requires;
				});

				it('should have es-ui.controllers as depedency', function () {
					expect(hasModule('es-ui.controllers')).toBe(true);
				});

				it('should have es-ui.services as depedency', function () {
					expect(hasModule('es-ui.services')).toBe(true);
				});

				it('should have es-ui.directives as depedency', function () {
					expect(hasModule('es-ui.directives')).toBe(true);
				});

				it('should have es-ui.templates as depedency', function () {
					expect(hasModule('es-ui.templates')).toBe(true);
				});

				it('should have ui.utils as depedency', function () {
					expect(hasModule('ui.utils')).toBe(true);
				});

				it('should have ui.router as depedency', function () {
					expect(hasModule('ui.router')).toBe(true);
				});
			});

		});
	});
});