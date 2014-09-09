define([
	'angular',
	'angularMocks'
], function(ng, mocks) {
	'use strict';

	describe('ES-UI Sprintf Service', function () {

		var module, service;
		beforeEach(function () {
			module = mocks.module('es-ui.services');
			mocks.inject(['SprintfService',
				function (s) {
					service = s;
				}
			]);
		});

		it('should exists', function () {
			expect(service).not.toBeNull();
			expect(service).not.toBeUndefined();
		});

		it('should contain format method', function () {
			expect(service.format).not.toBeUndefined();
		});

		it('should return same text that was passed when parameters does not exists', function () {
			var expected = 'some text';
			var result = service.format(expected);

			expect(result).toBe(expected);
		});

		it('should format message with one parameter', function () {
			var expected = 'some text';
			var result = service.format('some %s', 'text');
			
			expect(result).toBe(expected);
		});

		it('should format message with more than one parameters', function () {
			var expected = 'some long text';
			var result = service.format('some %s %s', 'long', 'text');

			expect(result).toBe(expected);
		});
	});
});