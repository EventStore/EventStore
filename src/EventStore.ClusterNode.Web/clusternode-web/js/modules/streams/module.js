/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================STREAMS============
            .state('streams', {
                url: 'streams',
                parent: 'app',
                templateUrl: 'streams.tpl.html',
                abstract: true
            })
            .state('streams.list', {
                url: '',
                templateUrl: 'streams.list.tpl.html',
                controller: 'StreamsListCtrl',
                data: {
                    title: 'Stream Browser'
                }
            })
            .state('streams.item', {
                url: '/{streamId}',
                templateUrl: 'streams.item.tpl.html',
                abstract: true,
                controller: ['$scope', '$stateParams', function ($scope, $stateParams) {
                    $scope.streamId = $stateParams.streamId;
                }],
                data: {
                    title: 'Stream'
                }
            })
            .state('streams.item.events', {
                url: '',
                templateUrl: 'streams.item.events.tpl.html',
                controller: 'StreamsItemEventsCtrl',
                data: {
                    title: 'Stream'
                }
            })
            .state('streams.item.navigation', {
                url: '/{position}/{type}/{count:[0-9]{1,99}}',
                //url: '/{position}/{type:\bbackward|forward\b}/{count:[0-9]{1,99}}',
                templateUrl: 'streams.item.events.tpl.html',
                controller: 'StreamsItemEventsCtrl',
                data: {
                    title: 'Stream'
                }
            })
            .state('streams.item.metadata', {
                url: '/metadata',
                views: {
                    '@streams': {
                        templateUrl: 'streams.item.event.tpl.html',
                        controller: 'StreamsItemEventCtrl'
                    }
                },
                data: {
                    metadata: true,
                    title: 'Stream Event'
                }
            })
            .state('streams.item.event', {
                url: '/{eventNumber:[0-9]{1,99}}',
                views: {
                    '@streams': {
                        templateUrl: 'streams.item.event.tpl.html',
                        controller: 'StreamsItemEventCtrl'
                    }
                },
                data: {
                    title: 'Stream Event'
                }
            })
            .state('streams.item.acl', {
                url: '/acl',
                views: {
                    '@streams': {
                        templateUrl: 'streams.item.acl.tpl.html',
                        controller: 'StreamsItemAclCtrl'
                    }
                },
                data: {
                    title: 'Edit Stream ACL'
                }

            });
    }]);
});