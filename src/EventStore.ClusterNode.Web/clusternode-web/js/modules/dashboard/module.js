/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================DASHBOARD============
            .state('dashboard', {
                parent: 'app',
                url: 'dashboard',
                templateUrl: 'dashboard.tpl.html',
                abstract: true,
                data: {
                    title: 'Dashboard'
                }
            })
            .state('dashboard.list', {
                url: '',
                templateUrl: 'dashboard.list.tpl.html',
                controller: 'DashboardListCtrl',
                
                data: {
                    title: 'Dashboard'
                }
            })
            .state('dashboard.snapshot', {
                url: '/snapshot',
                templateUrl: 'dashboard.snapshot.tpl.html',
                controller: 'DashboardSnaphostCtrl',
                data: {
                    title: 'Dashboard Snapshot'
                }
            });
    }]);
});