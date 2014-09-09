'use strict';

var gulp = require('gulp');
//var gutil = require('gulp-util');
var connect = require('gulp-connect');
//var rjs = require('gulp-requirejs');
var concat = require('gulp-concat');
var htmlmin = require('gulp-htmlmin');
var ngHtml2Js = require('gulp-ng-html2js');
var wrap = require('gulp-wrap');
var jshint = require('gulp-jshint');
var karma = require('gulp-karma');
var cache = require('gulp-cached');
var bower = require('gulp-bower');

var paths = {
    app: {
        templatesSource: './clusternode-web/views/*.tpl.html',
        templatesDestination: './clusternode-web/js/templates',
        source: './clusternode-web/js/**/*.js'
    },
    all: [
        './clusternode-web/js/**/*.js',
        './test/**/*.js'
    ],
    tests: './test/**/*.js'
};

gulp.task('bower', function() {
    bower();
});

/**
 * Creates JS version of HTML tpl files used
 * by ES UI
 **/

function templateForModule (moduleSource, moduleDest, moduleName) {
    gulp.src(moduleSource)
        .pipe(cache('html'))
        .pipe(htmlmin({
            //collapseBooleanAttributes: true,
            collapseWhitespace: true,
            removeAttributeQuotes: true,
            removeComments: true,
            removeEmptyAttributes: true,
            removeRedundantAttributes: true,
            removeScriptTypeAttributes: true,
            removeStyleLinkTypeAttributes: true
        }))
        .pipe(ngHtml2Js({
            moduleName: moduleName,
            prefix: ''
        }))
        .pipe(concat('templates.js'))
        .pipe(wrap('define([\'angular\'], function (angular) {\'use strict\'; <%= contents %> });'))
        .pipe(gulp.dest(moduleDest));
}

gulp.task('html', function () {
	templateForModule('./clusternode-web/views/*.tpl.html', 
        './clusternode-web/js/templates', 
        'es-ui.templates');
    templateForModule('./clusternode-web/js/modules/projections/views/*.tpl.html',
        './clusternode-web/js/modules/projections/templates',
        'es-ui.projections.templates');
    templateForModule('./clusternode-web/js/modules/security/views/*.tpl.html',
        './clusternode-web/js/modules/security/templates',
        'es-ui.security.templates');
    templateForModule('./clusternode-web/js/modules/dashboard/views/*.tpl.html',
        './clusternode-web/js/modules/dashboard/templates',
        'es-ui.dashboard.templates');
    templateForModule('./clusternode-web/js/modules/streams/views/*.tpl.html',
        './clusternode-web/js/modules/streams/templates',
        'es-ui.streams.templates');
    templateForModule('./clusternode-web/js/modules/users/views/*.tpl.html',
        './clusternode-web/js/modules/users/templates',
        'es-ui.users.templates');
    templateForModule('./clusternode-web/js/modules/admin/views/*.tpl.html',
        './clusternode-web/js/modules/admin/templates',
        'es-ui.admin.templates');
});

/**
 * Executes JS Hint verification against code
 **/
gulp.task('lint', function() {

    // we are only intereseted in our app src and test code
    return gulp.src(paths.all)
        .pipe(cache('linting'))
        .pipe(jshint({
            // load .jshintrc file
            lookup: true
        }))
        .pipe(jshint.reporter('jshint-stylish'));
});

/**
 * Executes Unit Tests
 **/
gulp.task('karma', function () {

    return gulp.src(['no need to supply files because everything is in config file'])
        .pipe(karma({
            configFile: 'config/karma.conf.js',
            action: 'watch'
        }));
});

/**
 * Dev task, executes watches for templates, js files (lint)
 * unit tests, and web browser to test "normally"
 **/
gulp.task('dev', function () {

    // whenever templates changes, re-run templates
    gulp.watch(paths.app.templatesSource, ['html']);
    // whenever code changes, re-run templates
    gulp.watch(paths.all, ['lint']);
    
    gulp.run('bower');
    gulp.run('lint');
    gulp.run('karma');
    gulp.run('connect');
});

gulp.task('watch-lint', function () {
    gulp.watch(paths.all, ['lint']);
    gulp.run('lint');
});


/**
 * Opens browser with application loaded
 **/
gulp.task('connect', connect.server({
    root: ['clusternode-web'],
    port: 8888,
    livereload: true,
    open: {
        browser: 'chrome'
    }
}));

gulp.task('default', ['dev']);
