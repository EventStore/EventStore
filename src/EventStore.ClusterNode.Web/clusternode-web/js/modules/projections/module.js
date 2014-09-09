/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================PROJECTIONS============
            .state('projections', {
                url: 'projections',
                parent: 'app',
                abstract: true,
                templateUrl: 'projections.tpl.html',
                data: {
                    title: 'Projections'
                }
            })
            .state('projections.list', {
                url: '',
                templateUrl: 'projections.list.tpl.html',
                controller: 'ProjectionsListCtrl',
                data: {
                    title: 'Projections'
                }
            })
            .state('projections.new', {
                url: '/new',
                templateUrl: 'projections.new.tpl.html',
                controller: 'ProjectionsNewCtrl',
                data: {
                    title: 'New Projection'
                }
            })

            .state('projections.standard', {
                url: '/standard',
                templateUrl: 'projections.standard.tpl.html',
                controller: 'ProjectionsStandardCtrl',
                data: {
                    title: 'New Standard Projection'
                }
            })
            .state('projections.item', {
                url: '/{location}',
                abstract: true,
                templateUrl: 'projections.item.tpl.html',
            })
            .state('projections.item.details', {
                url: '',
                templateUrl: 'projections.item.details.tpl.html',
                controller: 'ProjectionsItemDetailsCtrl',
                data: {
                    title: 'Projection Details'
                }
            })
            .state('projections.item.delete', {
                url: '/delete',
                templateUrl: 'projections.item.delete.tpl.html',
                controller: 'ProjectionsItemDeleteCtrl',
                data: {
                    title: 'Projection Delte'
                }
            })
            .state('projections.item.debug', {
                url: '/debug',
                templateUrl: 'projections.item.debug.tpl.html',
                controller: 'ProjectionsItemDebugCtrl',
                data: {
                    title: 'Projection Debug'
                }
            })
            .state('projections.item.edit', {
                url: '/edit',
                templateUrl: 'projections.item.edit.tpl.html',
                controller: 'ProjectionsItemEditCtrl',
                data: {
                    title: 'Projection Edit'
                }
            })

            // ========================================QUERY============
            .state('query', {
                url: 'query',
                parent: 'app',
                templateUrl: 'query.tpl.html',
                controller: 'QueryCtrl',
                data: {
                    title: 'Query'
                }
            });
    }]);
});