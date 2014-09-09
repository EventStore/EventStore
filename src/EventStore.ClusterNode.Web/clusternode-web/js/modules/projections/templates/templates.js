define(['angular'], function (angular) {'use strict'; (function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.item.debug.tpl.html',
    '<header class=page-header><h2 class=page-title>Projection Debug</h2><ul class=page-nav><li class=page-nav__item><button ng-click=runStep() ng-disabled=isRunning>Run Step</button></li><li class=page-nav__item><button ng-click=stop() ng-disabled=!isRunning>Stop</button></li><li class=page-nav__item><button ng-click=update()>Update</button></li><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><h3>Current Status: <small>{{statusInfo}}</small></h3><div class=container><div class=container-left><h3 class=block-title>Source</h3><div ui-ace=aceConfig ng-model=query></div></div><div class=container-right><h3 class=block-title>State {{partition}}</h3><pre>\n' +
    '{{ state | json }}\n' +
    '				</pre><h3 class=block-title>Events</h3><div ui-ace=aceEventsConfig readonly=true ng-model=events></div></div></div><p>NOT SURE IF THIS SHOULD BE REPEATED</p><ul class=page-nav><li class=page-nav__item><button ng-click=update()>Update</button></li><li class=page-nav__item><button ng-click=runStep() ng-disabled=!isRunning>Run Step</button></li></ul><div class=container><div class=container-left><div class=block><h3 class=block-title>Instructions</h3><ol><li>Make sure you use the Google Chrome browser.</li><li>Open a debugger (<kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>J</kbd> on Windows).</li><li>Check the debugger console for errors (occurred while running the projection definition on while loading state and events).</li><li>Click the ‘Run’ button above.</li><li>Watch for errors in the debugger console</li><li>Check the debugging status on XXXXXXXXX.</li><li>Execution will stop on ‘debugger’ statement immediately before invoking your event handler.</li></ol><p>Any log output and emitted events will appear in the log viewer.</p></div></div><div class=container-right><h3 class=block-title>Log Viewer</h3><div es-proj-debug-frame="" es-location=location></div></div></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.item.delete.tpl.html',
    '<header class=page-header><h2 class=page-title>Delete Projection</h2><ul class=page-nav><li class=page-nav__item><a ng-click=remove($event) href=#>Confirm Delete</a> <a ui-sref=^.details>Back</a></li></ul></header><table><tbody><tr><td>Name</td><td><span>{{ projection.name }}</span></td></tr><tr><td>State</td><td><span>{{ projection.state }}</span></td></tr><tr><td>Source</td><td><pre>\n' +
    '{{ projection.source }}\n' +
    '				</pre></td></tr><tr><td></td><td><label><input type=checkbox ng-model=deleteCheckpoint>Delete Checkpoint Stream</label></td></tr><tr><td></td><td><label><input type=checkbox ng-model=deleteState>Delete State Stream</label></td></tr></tbody></table>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.item.details.tpl.html',
    '<header class=page-header><h2 class=page-title>Projection Details</h2><ul class=page-nav><li class=page-nav__item><button ng-click=start() ng-disabled=isRunning href=#>Start</button></li><li class=page-nav__item><button ng-click=stop() ng-disabled=isStopped>Stop</button></li><li class=page-nav__item><a ui-sref=^.edit>Edit</a></li><li class=page-nav__item><a ui-sref=^.debug>Debug</a></li><li class=page-nav__item><a ui-sref=^.delete>Delete</a></li><li class=page-nav__item><button ng-click=reset($event)>Reset</button></li><li class=page-nav__item><a ui-sref=^.^.list>Back</a></li></ul></header><h3>{{stats.name}} - {{stats.status}}</h3><p><strong>mode:</strong><span>{{stats.mode}}</span></p><p style="background-color: red; color: white" ng-if=stats.stateReason>{{stats.stateReason}}</p><div class=container><div class=container-left><h3 class=block-title>Source</h3><div readonly=true ui-ace=aceConfig ng-model=query></div></div><div class=container-right><h3 class=block-title>Stats</h3><table><tr><td>Events/sec</td><td>{{ stats.eventsPerSecond }}</td></tr><tr><td>Buffered events</td><td>{{ stats.bufferedEvents }}</td></tr><tr><td>Events processed</td><td>{{ stats.eventsProcessedAfterRestart }}</td></tr><tr><td>Partitions cached</td><td>{{ stats.partitionsCached }}</td></tr><tr><td>Reads in-progress</td><td>{{ stats.readsInProgress }}</td></tr><tr><td>Writes in-progress</td><td>{{ stats.writesInProgress }}</td></tr><tr><td>Write queue</td><td>{{ stats.writePendingEventsAfterCheckpoint }}</td></tr><tr><td>Write queue (chkp)</td><td>{{ stats.writePendingEventsBeforeCheckpoint }}</td></tr><tr><td>Checkpoint status</td><td>{{ stats.checkpointStatus }}</td></tr><tr><td>Position</td><td>{{ stats.position }}</td></tr><tr><td>Last checkpoint</td><td>{{ stats.lastCheckpoint }}</td></tr><table><h3 class=block-title>Results <span ng-show=stream>(<a ui-sref="streams.item.events({streamId: stream})">{{ stream }}</a>)</span></h3><pre>\n' +
    '{{result | json}}\n' +
    '		</pre><h3 class=block-title>State</h3><pre>\n' +
    '{{state | json}}\n' +
    '		</pre></table></table></div></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.item.edit.tpl.html',
    '<header class=page-header><h2 class=page-title>Projection Edit</h2><ul class=page-nav><li class=page-nav__item><button ng-click=start() ng-disabled=isRunning href=#>Start</button></li><li class=page-nav__item><button ng-click=stop() ng-disabled=isStopped>Stop</button></li><li class=page-nav__item><button ng-click=save()>Save</button></li><li class=page-nav__item><button ng-click=reset($event)>Reset</button></li><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><h3>{{stats.name}} - {{stats.status}}</h3><p><strong>mode:</strong><span>{{stats.mode}}</span></p><p style="background-color: red; color: white" ng-if=stats.stateReason>{{stats.stateReason}}</p><div class=container><div class=container-left><h3 class=block-title>Source</h3><div ui-ace=aceConfig ng-model=query></div><label><input type=checkbox ng-model=emit>Emit enabled</label></div><div class=container-right><h3 class=block-title>Results <span ng-show=stream>(<a ui-sref="streams.item.events({streamId: stream})">{{ stream }}</a>)</span></h3><pre>\n' +
    '{{result | json}}\n' +
    '		</pre><h3 class=block-title>State</h3><pre>\n' +
    '{{state | json}}\n' +
    '		</pre></div></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.item.tpl.html',
    '<div ui-view=""></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.list.tpl.html',
    '<header class=page-header><h2 class=page-title>Projections</h2><ul class=page-nav><li class=page-nav__item><a ng-click=disableAll($event)>Disable All</a></li><li class=page-nav__item><a ng-click=enableAll($event)>Enable All</a></li><li class=page-nav__item><input type=checkbox ng-model=includeQueries>Include Queries</li><li class=page-nav__item><a ui-sref=^.new>New Projection</a></li></ul></header><table class=table-projections><thead><tr><th>Name</th><th>Status</th><th>Mode</th><th>Done</th><th>Read / Write in Progress</th><th>Write Queues</th><th>Partitions Cached</th><th>Rate (events/s)</th><th colspan=2>Events</th></tr><tr><th></th><th></th><th></th><th></th><th></th><th></th><th></th><th></th><th class=table-subheading>Processed</th><th class=table-subheading>Buffered</th></tr></thead><tbody><tr ng-repeat="projection in projections"><td><a ui-sref="^.item.details({location: projection.location})">{{ projection.name }}</a></td><td>{{ projection.status }}{{ projection.checkpointStatus }}</td><td>{{ projection.mode }}</td><td>{{ projection.progress.toFixed(1) }}%</td><td>{{ projection.readsInProgress }} / {{ projection.writesInProgress }}</td><td>{{ projection.writePendingEventsAfterCheckpoint }} / {{ projection.writePendingEventsBeforeCheckpoint }}</td><td>{{ projection.partitionsCached }}</td><td>{{ projection.eventsPerSecond }}</td><td>{{ projection.eventsProcessedAfterRestart }}</td><td>{{ projection.bufferedEvents }}</td></tr><tr ng-hide=projections><td colspan=10><em>No projections</em></td></tr></tbody></table>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.new.tpl.html',
    '<header class=page-header><h2 class=page-title>New Projection</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.list>Back</a></li></ul></header><div><a ui-sref=^.standard>You can also post a standard projection</a></div><form novalidate="" name=newProj ng-submit=save() class=new-projection-form><table><tbody><tr><td>Name</td><td><input ng-model=name required="" class=form-table name=projName ng-class="{ \'form-table--error\' : newProj.projName.$invalid && !newProj.projName.$pristine }"></td></tr><tr><td>Source</td><td><div ui-ace=aceConfig required="" ng-model=source></div></td></tr><tr><td>Mode</td><td><select ng-model=mode class=form-table ng-options="obj.value as obj.name for obj in modes"></select></td></tr><tr><td>Checkpoints Enabled</td><td><input type=checkbox ng-model=checkpoints ng-disabled=checkpointsDisabled></td></tr><tr><td>Emit Enabled</td><td><input type=checkbox ng-model=emit></td></tr><tr><td>Enabled</td><td><input type=checkbox ng-model=enabled></td></tr></tbody></table><ul class=page-nav><li class=page-nav__item><button type=submit ng-disabled=newProj.$invalid>Create</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.standard.tpl.html',
    '<header class=page-header><h2 class=page-title>New Standard Projection</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.list>Back</a></li></ul></header><form novalidate="" name=newProj ng-submit=save()><table><tbody><tr><td>Name</td><td><input ng-model=name required="" class=form-table name=projName ng-class="{ \'form-table--error\' : newProj.projName.$invalid && !newProj.projName.$pristine }"></td></tr><tr><td>Source</td><td><div ui-ace=aceConfig required="" ng-model=source></div></td></tr><tr><td>Select Type</td><td><select ng-model=type required="" class=form-table ng-options="obj.value as obj.name for obj in types"></select></td></tr></tbody></table><ul class=page-nav><li class=page-nav__item><button type=submit ng-disabled=newProj.$invalid>Create</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('projections.tpl.html',
    '<div ui-view=""></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.projections.templates');
} catch (e) {
  module = angular.module('es-ui.projections.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('query.tpl.html',
    '<header class=page-header><h2 class=page-title>Query</h2><ul class=page-nav><li class=page-nav__item><button ng-click=run()>Run</button></li><li class=page-nav__item><button ng-click=stop() ng-disabled=disableStop()>Break</button></li><li class=page-nav__item><button ng-click=debug() ng-disabled=!isCreated>Debug</button></li></ul></header><h3 ng-visible=status>{{status}}</h3><div class=container><div class=container-left><h3 class=block-title>Source</h3><div ui-ace=aceConfig ng-model=query></div></div><div class=container-right><h3 class=block-title>State</h3><pre>\n' +
    '{{state | json}}\n' +
    '		</pre></div></div>');
}]);
})();
 });