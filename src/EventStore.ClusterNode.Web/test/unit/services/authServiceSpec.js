define([
	'angular',
	'angularMocks'
], function(ng, mocks) {
	'use strict';

	describe('ES-UI Auth Service', function () {

		var module, service, cookieStore, base64, http;
		beforeEach(function () {
			module = mocks.module('es-ui.services');

			mocks.inject([
				'$httpBackend',
				'Base64',
				'$cookieStore',
				'AuthService',
				function (h, b, c, s) {
					service = s;
					cookieStore = c;
					base64 = b;
					http = h;

					spyOn(cookieStore, 'get');
					spyOn(cookieStore, 'put');
				}
			]);
		});

		it('should exists', function () {
			expect(service).not.toBeNull();
			expect(service).not.toBeUndefined();
		});

		it('should contains setCredentials method', function () {
			expect(service.setCredentials).not.toBeUndefined();
		});

		it('should contains clearCredentials method', function () {
			expect(service.clearCredentials).not.toBeUndefined();
		});

		it('should set authorization header to basic on load',
			mocks.inject(['$http', function ($http) {
				//http.flush();
				expect($http.defaults.headers.common['Authorization']).toBe('Basic ');
			}])
		);

		it('should get authdata from cookie store on load', function () {
			expect(cookieStore.get).toHaveBeenCalledWith('authdata');
		});
	});
});