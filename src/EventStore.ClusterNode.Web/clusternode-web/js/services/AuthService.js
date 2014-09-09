/*jshint sub: true*/
define(['./_module'], function (app) {

	'use strict';

	// http://wemadeyoulook.at/en/blog/implementing-basic-http-authentication-http-requests-angular/
	return app.factory('AuthService', [
		'Base64', 
		'$q',
		'$cookieStore', 
		'$http', 
		'$rootScope',
		//'baseUrl', // not sure why by this does not work for services
		function (Base64, $q, $cookieStore, $http, $rootScope) {
			// initialize to whatever is in the cookie, if anything
			var authdata = $cookieStore.get('authdata');
			if(authdata) {
				setBaseUrl(authdata.server);
				authdata = authdata.encoded;
			}
			$http.defaults.headers.common['Authorization'] = 'Basic ' + (authdata || '');
 
			function setBaseUrl (url) {
				$rootScope.baseUrl = url;
				//baseUrl = { url: url };
			}

			function getBaseUrl () {
				return $rootScope.baseUrl;
			}

 			function prepareUrl (str) {
				if(str.indexOf('/', str.length - '/'.length) !== -1) {
	            	str = str.substring(0, str.length - 1);
	            }

	            if(str.indexOf('http') === -1) {
	            	str = 'http://' + str;
	            }

	            return str;
			}

		    return {
		        setCredentials: function (username, password, server) {
		            var encoded = Base64.encode(username + ':' + password);
		            $http.defaults.headers.common.Authorization = 'Basic ' + encoded;

		            server = prepareUrl(server);
		            setBaseUrl(server);

		            $cookieStore.put('authdata', {
		            	encoded: encoded,
		            	server: server
		            });
		        },
		        clearCredentials: function () {
		            document.execCommand('ClearAuthenticationCache');
		            $cookieStore.remove('authdata');
		            $http.defaults.headers.common.Authorization = 'Basic ';
		        },
		        existsAndValid: function () {
		        	var deferred = $q.defer();
		        	var data = $cookieStore.get('authdata');
		        	
		        	if(!data) {
		        		deferred.reject('Data does not exists');
		        	} else {
		        		this.validate(data.encoded, data.server)
		        		.success(function() {
		        			deferred.resolve();
		        		})
		        		.error(function (){
		        			deferred.reject('Wrong credentials or server not exists');
		        		});
		        	}

		        	return deferred.promise;
		        },
		        validate: function (username, password, server) {
		        	var encoded;
		        	if(!server) {
		        		server = password;
		        		encoded = username;
		        	} else {
		        		encoded = Base64.encode(username + ':' + password);
		        	}

		            server = prepareUrl(server);
		        	return $http.get(server + '/new-guid', {
		        		headers: {
		        			'Accept': '*/*',
		        			Authorization: 'Basic ' + encoded
		        		}
		        	});
		        }
		    };
		}
	]);
});