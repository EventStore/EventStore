define(['angular'], function (angular) {'use strict'; (function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.item.acl.tpl.html',
    '<header class=page-header><h2 class=page-title>Edit ACL for Event Stream \'{{ streamId }}\'</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.events>Back</a></li></ul></header><form novalidate="" name=editAcl ng-submit=updateAcl()><table><tbody><tr><td>Reader</td><td><input name=reader class=form-table ng-model=reader></td></tr><tr><td>Writer</td><td><input name=writer class=form-table ng-model=writer></td></tr><tr><td>Deleter</td><td><input name=deleter class=form-table ng-model=deleter></td></tr><tr><td>Meta Reader</td><td><input name=metareader class=form-table ng-model=metareader></td></tr><tr><td>Meta Writer</td><td><input name=metawriter class=form-table ng-model=metawriter></td></tr></tbody></table><ul><li><button type=submit>Save</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.item.event.tpl.html',
    '<header class=page-header><h2 class=page-title>{{ evt.title }} <small ng-if=isNotTheSame>Link from {{ evt.positionEventNumber + \'@\' + evt.positionStreamId }}</small></h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.events>Back</a></li></ul></header><ul><li ng-if=next><a ui-sref=".({streamId: evt.streamId, eventNumber: evt.positionEventNumber + 1})">next</a></li><li ng-if=prev><a ui-sref=".({streamId: evt.streamId, eventNumber: evt.positionEventNumber - 1})">prev</a></li><li>NOT SURE WHAT EDIT/ALERNATE SHOULD DO</li><li ng-repeat="link in links"><a href=#>{{ link.relation }}</a></li></ul><table><thead><tr><th>No</th><th>Stream</th><th>Type</th><th>Timestamp</th></tr></thead><tbody><tr><td>{{ evt.eventNumber }}</td><td><a ui-sref="^.events({streamId: evt.streamId})">{{ evt.streamId }}</a></td><td>{{ evt.eventType }}</td><td>{{ evt.updated | date:\'yyyy-MM-dd HH:mm\'}}</td></tr><tr ng-if="evt.isJson || evt.isMetaData || evt.isLinkMetaData"><td colspan=4><div ng-if=evt.isJson><strong>Data</strong><pre>\n' +
    '{{ evt.data }}\n' +
    '				</pre></div><div ng-if=evt.isMetaData><strong>Metdata</strong><pre>\n' +
    '{{ evt.metaData }}\n' +
    '				</pre></div><div ng-if=evt.isLinkMetaData><strong>Link metadata</strong><pre>\n' +
    '{{ evt.content.linkMetaData }}\n' +
    '				</pre></div></td></tr></tbody></table>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.item.events.tpl.html',
    '<table><thead><tr><th>Event #</th><th>Name</th><th>Type</th><th>Created Date</th><th></th></tr></thead><tbody ng-repeat="event in streams"><tr><td><a ui-sref="^.event({streamId: event.streamId, eventNumber: event.positionEventNumber})">{{ event.positionEventNumber }}</a></td><td><a ui-sref="^.event({streamId: event.streamId,eventNumber: event.positionEventNumber})">{{ event.title }}</a></td><td>{{ event.eventType }}</td><td>{{ event.updated | date:\'yyyy-MM-dd HH:mm\'}}</td><td><a ng-click="toggleJson($event, event)" style="cursor: pointer" ng-if="event.isJson || event.isLinkMetaData || event.isMetaData">JSON</a></td></tr><tr ng-show=event.showJson><td colspan=5><div ng-if=event.isJson><strong>Data</strong><pre>\n' +
    '{{ event.data }}					\n' +
    '					</pre></div><div ng-if=event.metaData><strong>Metadata</strong><pre>\n' +
    '{{ event.metaData }}					\n' +
    '					</pre></div><div ng-if=event.isLinkMetaData><strong>Link metadata</strong><pre>\n' +
    '{{ event.linkMetaData }}					\n' +
    '					</pre></div></td></tr></tbody><tbody ng-hide=streams><tr><td colspan=5><em>No events for current path: {{ $stateParams | json }}</em></td></tr></tbody></table>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.item.tpl.html',
    '<header class=page-header><h2 class=page-title>Event Stream \'{{ streamId }}\'</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=.acl>Edit ACL</a></li><li class=page-nav__item><a ui-sref=^.list>Back</a></li></ul></header><ul><li ng-repeat="link in links"><a ng-href={{link.uri}}>{{ link.relation }}</a> </li></ul><div ui-view="" es-link-header=""></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.list.tpl.html',
    '<header class=page-header><h2 class=page-title>Stream Browser</h2><ul class=page-nav><li class=page-nav__item><form><input ui-keypress="{\'enter\': \'gotoStream($event)\'}" ng-model=search></form></li></ul></header><div class=container><div class=container-left><table><thead><tr><th>Recently Created Streams <small>issue: #1</small></th></tr></thead><tbody><tr ng-repeat="stream in createdStreams"><td><a ui-sref="^.item.events({streamId: stream.streamId})">{{ stream.streamId }}</a></td></tr><tr ng-hide=createdStreams><td><em>No recently created streams</em></td></tr></tbody></table></div><div class=container-right><table><thead><tr><th>Recently Changed Streams</th></tr></thead><tbody><tr ng-repeat="stream in changedStreams"><td><a ui-sref="^.item.events({streamId: stream.streamId})">{{ stream.streamId }}</a></td></tr><tr ng-hide=changedStreams><td><em>No recently changed streams</em></td></tr></tbody></table></div></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.streams.templates');
} catch (e) {
  module = angular.module('es-ui.streams.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('streams.tpl.html',
    '<div ui-view=""></div>');
}]);
})();
 });