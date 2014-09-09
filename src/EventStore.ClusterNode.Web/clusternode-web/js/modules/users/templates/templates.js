define(['angular'], function (angular) {'use strict'; (function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.delete.tpl.html',
    '<header class=page-header><h2 class=page-title>Delete User {{ user.loginName }}</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><pre>\n' +
    '{{ user | json }}\n' +
    '</pre><ul><li><a href=# ng-click=confirm($event) ng-disabled=disable>Delete</a></li></ul>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.details.tpl.html',
    '<div ui-view=""><header class=page-header><h2 class=page-title>User Details {{ user.loginName }}</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.edit>Edit</a></li><li class=page-nav__item><a ui-sref=^.delete>Delete</a></li><li class=page-nav__item ng-hide=user.disabled><a ui-sref=^.disable>Disable</a></li><li class=page-nav__item ng-show=user.disabled><a ui-sref=^.enable>Enable</a></li><li class=page-nav__item><a ui-sref=^.reset>Password reset</a></li><li class=page-nav__item><a ui-sref=^.^.list>Back</a></li></ul></header><pre>\n' +
    '{{ user | json }}\n' +
    '</pre></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.disable.tpl.html',
    '<header class=page-header><h2 class=page-title>Disable User {{ user.loginName }}</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><pre>\n' +
    '{{ user | json }}\n' +
    '</pre><ul><li><a href=# ng-click=confirm($event) ng-disabled=disable>Disable</a></li></ul>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.edit.tpl.html',
    '<header class=page-header><h2 class=page-title>Edit User {{ user.loginName }}</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><form novalidate="" name=editUsr ng-submit=confirm()><table><tbody><tr><td>Login Name</td><td>{{ user.loginName}}</td></tr><tr><td>Full Name</td><td><input name=fullName class=form-table ng-class="{ \'form-table--error\' : editUsr.fullName.$invalid && !editUsr.fullName.$pristine }" ng-model=fullName required=""></td></tr><tr><td>Groups</td><td><input type=checkbox name=isAdmin ng-model=isAdmin><label for=isAdmin>Is Administrator</label></td></tr></tbody></table><ul><li><button type=submit ng-disabled=editUsr.$invalid>Update</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.enable.tpl.html',
    '<header class=page-header><h2 class=page-title>Enable User {{ user.loginName }}</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><pre>\n' +
    '{{ user | json }}\n' +
    '</pre><ul><li><a href=# ng-click=confirm($event) ng-disabled=disable>Enable</a></li></ul>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.reset.tpl.html',
    '<header class=page-header><h2 class=page-title>Reset User {{ user.loginName }} Password</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.details>Back</a></li></ul></header><form novalidate="" name=resetPwd ng-submit=confirm()><table><tbody><tr><td>Login Name</td><td>{{ user.loginName}}</td></tr><tr><td>Password</td><td><input type=password name=password class=form-table ng-class="{ \'form-table--error\' : resetPwd.password.$invalid && !resetPwd.password.$pristine }" ng-model=password required=""></td></tr><tr><td>Confirm Password</td><td><input type=password class=form-table name=confirmPassword ng-model=confirmPassword ng-class="{ \'form-table--error\' : resetPwd.confirmPassword.$invalid && !resetPwd.confirmPassword.$pristine }" es-validate-equals=password></td></tr></tbody></table><ul><li><button type=submit ng-disabled=resetPwd.$invalid>Reset password</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.item.tpl.html',
    '<div ui-view=""></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.list.tpl.html',
    '<div ui-view=""><header class=page-header><h2 class=page-title>Users</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.new>New User</a></li></ul></header><table class=table-queues><thead><tr><th>Login Name</th><th>Full Name</th><th>Groups</th><th>Disabled</th><th>Updated</th></tr></thead><tbody><tr ng-repeat="user in users"><td><a ui-sref="^.item.details({username: user.loginName})">{{ user.loginName }}</a></td><td>{{ user.fullName }}</td><td>{{ user.groups.join(\', \') }}</td><td>{{ user.disabled }}</td><td>{{ user.dateLastUpdated | date:\'short\' }}</td></tr><tr ng-hide=users><td colspan=5><em>No users</em></td></tr></tbody></table></div>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.new.tpl.html',
    '<header class=page-header><h2 class=page-title>New User</h2><ul class=page-nav><li class=page-nav__item><a ui-sref=^.list>Back</a></li></ul></header><form novalidate="" name=newUsr ng-submit=confirm()><table><tbody><tr><td>Login Name</td><td><input class=form-table autofocus="" required="" name=loginName ng-class="{ \'form-table--error\' : newUsr.loginName.$invalid && !newUsr.loginName.$pristine }" ng-model=newUser.loginName></td></tr><tr><td>Full Name</td><td><input ng-class="{ \'form-table--error\' : newUsr.fullName.$invalid && !newUsr.fullName.$pristine }" name=fullName class=form-table required="" ng-model=newUser.fullName></td></tr><tr><td>Password</td><td><input type=password ng-class="{ \'form-table--error\' : newUsr.password.$invalid && !newUsr.password.$pristine }" name=password class=form-table ng-model=newUser.password required=""></td></tr><tr><td>Confirm Password</td><td><input type=password ng-class="{ \'form-table--error\' : newUsr.confirmPassword.$invalid && !newUsr.confirmPassword.$pristine }" name=confirmPassword class=form-table required="" ng-model=newUser.confirmPassword es-validate-equals=newUser.password></td></tr><tr><td>Groups</td><td><input type=checkbox name=isAdmin ng-model=newUser.isAdmin><label for=isAdmin>Is Administrator</label></td></tr></tbody></table><ul><li><button type=submit ng-disabled=newUsr.$invalid>Create</button></li></ul></form>');
}]);
})();

(function(module) {
try {
  module = angular.module('es-ui.users.templates');
} catch (e) {
  module = angular.module('es-ui.users.templates', []);
}
module.run(['$templateCache', function($templateCache) {
  $templateCache.put('users.tpl.html',
    '<div ui-view=""></div>');
}]);
})();
 });