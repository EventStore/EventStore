/* global define */
/*jshint sub: true */

define(['es-ui'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider', '$urlRouterProvider', '$httpProvider', '$provide',
    function ($stateProvider, $urlRouterProvider, $httpProvider, $provide) {

        // http://jsfiddle.net/Zenuka/pHEf9/
        $provide.decorator('$q', ['$delegate', function ($delegate) {
            var $q = $delegate;

            // Extention for q
            $q.allSettled = $q.allSettled || function (promises) {
                var deferred = $q.defer();
                if (!angular.isArray(promises)) {
                    throw 'allSettled can only handle an array of promises (for now)';
                }

                var states = [];
                var results = [];
                var didAPromiseFail = false;

                // First create an array for all promises setting their state to false (not completed)
                angular.forEach(promises, function (promise, key) {
                    states[key] = false;
                });

                // Helper to check if all states are finished
                var checkStates = function (states, results, deferred, failed) {
                    var allFinished = true;
                    angular.forEach(states, function (state) {
                        if (!state) {
                            allFinished = false;
                            return;
                        }
                    });
                    if (allFinished) {
                        if (failed) {
                            deferred.reject(results);
                        } else {
                            deferred.resolve(results);
                        }
                    }
                };

                // Loop through the promises
                // a second loop to be sure that checkStates is called when all states are set to false first
                angular.forEach(promises, function (promise, key) {
                    $q.when(promise).then(function (result) {
                        states[key] = true;
                        results[key] = result;
                        checkStates(states, results, deferred, didAPromiseFail);
                    }, function (reason) {
                        states[key] = true;
                        results[key] = reason;
                        didAPromiseFail = true;
                        checkStates(states, results, deferred, didAPromiseFail);
                    });
                });

                return deferred.promise;
            };

            return $q;
        }]);
    }]);
});