/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================SECURITY============
            .state('signin', {
                url: '/',
                templateUrl: 'signin.tpl.html',
                controller: 'SignInCtrl',
                data: {
                    title: 'Sign in'
                }
            })
            .state('signout', {
                url: 'signout',
                parent: 'app',
                template: '<div ui-view></div>',
                controller: 'SignOutCtrl',
                data: {
                    title: 'Sign out'
                }
            });
    }]);
});