/* global define */
/*jshint sub: true */

define(['./_index'], function (app) {
	'use strict';

    return app.config([
    '$stateProvider',
    function ($stateProvider) {

        $stateProvider
            // ========================================USERS============
            .state('users', {
                url: 'users',
                parent: 'app',
                templateUrl: 'users.tpl.html',
                abstract: true,
                data: {
                    title: 'Users'
                }
            })
            .state('users.list', {
                url: '',
                templateUrl: 'users.list.tpl.html',
                controller: 'UsersListCtrl',
                data: {
                    title: 'Users'
                }
            })
            .state('users.new', {
                url: '/new',
                templateUrl: 'users.new.tpl.html',
                controller: 'UsersNewCtrl',
                data: {
                    title: 'New User'
                }
            })
            .state('users.item', {
                url: '/{username}',
                abstract: true,
                templateUrl: 'users.item.tpl.html',
                data: {
                    title: 'User Details'
                }
            })
            .state('users.item.details', {
                url: '',
                templateUrl: 'users.item.details.tpl.html',
                controller: 'UsersItemDetailsCtrl',
                data: {
                    title: 'User Details'
                }
            })
            .state('users.item.edit', {
                url: '/edit',
                templateUrl: 'users.item.edit.tpl.html',
                controller: 'UsersItemEditCtrl',
                data: {
                    title: 'Edit User'
                }
            })
            .state('users.item.disable', {
                url: '/disable',
                templateUrl: 'users.item.disable.tpl.html',
                controller: 'UsersItemDisableCtrl',
                data: {
                    title: 'Disable User'
                }
            })
            .state('users.item.enable', {
                url: '/enable',
                templateUrl: 'users.item.enable.tpl.html',
                controller: 'UsersItemEnableCtrl',
                data: {
                    title: 'Enable User'
                }
            })
            .state('users.item.delete', {
                url: '/delete',
                templateUrl: 'users.item.delete.tpl.html',
                controller: 'UsersItemDeleteCtrl',
                data: {
                    title: 'Delete User'
                }
            })
            .state('users.item.reset', {
                url: '/reset',
                templateUrl: 'users.item.reset.tpl.html',
                controller: 'UsersItemResetCtrl',
                data: {
                    title: 'Reset User Password'
                }
            });
    }]);
});