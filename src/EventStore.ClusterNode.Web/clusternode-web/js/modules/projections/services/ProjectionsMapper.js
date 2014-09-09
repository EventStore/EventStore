define(['./_module'], function (app) {

	'use strict';

	return app.provider('ProjectionsMapper', function () {

		var lastRequest = {},
			lastTimestamp;

		function map (data) {
			var proj = data.projections, 
				current = {}, 
				currentTime = new Date(), 
				i = 0, 
				length = proj.length,
				item,
				eventProcessed,
				elapsedTime,
				last,
				name;

			proj.sort(function (a, b) {
				return a.name.localeCompare(b.name);
			});

			for(; i < length; i++) {
				item = proj[i];
				name = item.name;
				last = lastRequest[name];
				current[name] = item;
				
				if(last === undefined || !lastTimestamp) {
					continue;
				}

				eventProcessed = item.eventsProcessedAfterRestart - last.eventsProcessedAfterRestart;
				elapsedTime = currentTime - lastTimestamp;
				item.eventsPerSecond = (1000.0 * eventProcessed / elapsedTime).toFixed(1);
				item.location = encodeURIComponent(item.statusUrl);
				proj[i] = item;
			}

			lastRequest = current;
			lastTimestamp = currentTime;
	        
	        return proj;
		}

		this.$get = [
			function () {
				return {
					map: map
				};
			}
		];
    });

});