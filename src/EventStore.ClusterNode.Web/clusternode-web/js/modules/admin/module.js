/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================ADMIN============
            .state('admin', {
                url: 'admin',
                parent: 'app',
                templateUrl: 'admin.tpl.html',
                controller: 'AdminCtrl',
                data: {
                    title: 'Admin'
                }
            });
    }]);
});