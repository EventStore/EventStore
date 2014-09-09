define(['./_module'], function (app) {

    'use strict';

	return app.factory('poller', [
		'$timeout', '$q',
		function ($timeout, $q)  {
			var tasks = [];

			function Task (opts) {
				this.opts = opts;
			}

			Task.prototype = {
				start: function () {
					var deferred = $q.defer(),
						self = this;

					(function tick () {

						var p = self.opts.action.apply(null, self.opts.params),
							rejected = false;

						p.then(function (data) {
							deferred.notify(data.data);
						}, function () {
							rejected = true;
							deferred.reject('Error occured');
						});

						self.timeoutId = $timeout(tick, self.opts.intevral);

					})();

					this.promise = deferred.promise;

					return this;
				},
				stop: function () {
					$timeout.cancel(this.timeoutId);
					this.timeoutId = null;
				},
				update: function (opts) {
					opts.intevral = opts.intevral || this.opts.intevral;
					opts.params = opts.params || this.opts.params;
					opts.intevral = opts.intevral || this.opts.intevral;
					opts.action = this.opts.action;
					this.opts = opts;
				}
			};

			function create (opts) {
				var task = new Task(opts);
				tasks.push(task);

				return task;
			}

			function stopAll () {
				var i = 0, len = tasks.length;
				for(; i < len; i++) {
					tasks[i].stop();
				}
			}

			function clear () {
				stopAll();
				tasks = [];
			}

			return {
				create: create,
				stopAll: stopAll,
				clear: clear
			};

	}]);
});