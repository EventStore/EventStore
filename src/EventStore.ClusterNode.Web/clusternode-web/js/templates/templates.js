define(['angular'], function (angular) {'use strict'; (function(module) {
try {
  module = angular.module('es-ui.templates');
} catch (e) {
  module = angular.module('es-ui.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('es-toggable-row.tpl.html',
    '<td ng-if=esQueue.show>{{ esQueue.queueName }} <a class=table-collapsetoggle>&minus;</a></td><td ng-if=!esQueue.show>{{ esQueue.queueName }} <a class=table-collapsetoggle>&plus;</a></td><td>{{ esQueue.lengthCurrentTryPeak }}</td><td>{{ esQueue.lengthLifetimePeak }}</td><td>{{ esQueue.avgItemsPerSecond }}</td><td>{{ esQueue.avgProcessingTime.toFixed(3)}}</td><td>{{ esQueue.busy }}</td><td>{{ esQueue.totalItemsProcessed }}</td><td style="text-align: right">n/a</td>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.templates');
} catch (e) {
  module = angular.module('es-ui.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('index.tpl.html',
    '<header class=site-header><h1 class=site-title><a href=#><img src=images/logo.svg height=29 width=119 alt="Event Store"></a></h1><ul class=site-nav><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'dashboard\') }"><a ui-sref=dashboard.list>Dashboard</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'streams\') }"><a ui-sref=streams.list>Stream Browser</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'projections\') }"><a ui-sref=projections.list>Projections</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'query\') }"><a ui-sref=query>Query</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'admin\') }"><a ui-sref=admin>Admin</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'users\') }"><a ui-sref=users.list>Users</a></li><li class=site-nav__item ng-class="{\'site-nav__item--active\': $state.includes(\'signout\') }"><a ui-sref=signout>Log Out</a></li></ul></header><main class=site-main><div ui-view=""></div></main><footer class=site-footer><p>Event Store 2.1 &middot; <a href="http://geteventstore.com/docs/" target=_blank>Documentation</a> &middot; <a href="http://geteventstore.com/support/" target=_blank>Support</a></p></footer>');
}]);
})();
 });