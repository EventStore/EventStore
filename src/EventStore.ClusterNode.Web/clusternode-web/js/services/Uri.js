define(['./_module'], function (app) {

    'use strict';

	return app.factory('uri', function () {

        var element;

        function unserialize (query) {
            var params = {};
            // http://stevenbenner.com/2010/03/javascript-regex-trick-parse-a-query-string-into-an-object/
            var rex = new RegExp('([^?=&]+)(=([^&]*))?', 'g');
            query.replace(rex, function($0, $1, $2, $3) {
                params[$1] = $3;
            });

            return params;
        }

        return {
            parse: function (url) {

                if(!element) {
                    element = document.createElement('a');
                }

                element.href = url;
                return {
                    hash: element.hash,
                    host: element.host,
                    hostname: element.hostname,
                    href: url,
                    pathname: element.pathname,
                    port: element.port,
                    protocol: element.protocol,
                    search: element.search,
                    file: element.pathname.split('/').pop(),
                    params: unserialize(element.search)
                };
            },
            getQuery: function (params) {
                var p, r = [];
                for(p in params) {
                    r.push(p + '=' + encodeURIComponent(params[p]));
                }

                return r.join('&');
            }
        };
    });
});