define(['./_module'], function (app) {

	'use strict';

	return app.provider('DashboardMapper', function () {

		function createEmptyGroup (groupName) {
			return {
                queueName: groupName,
                groupName: groupName,
                length: 0,
                avgItemsPerSecond: 0,
                avgProcessingTime: 0.0,
                idleTimePercent: 0.0,
                lengthCurrentTryPeak: 0,
                lengthLifetimePeak: 0,
                totalItemsProcessed: 0,
                groupQueues: 0,
                queues: []
			};
		}

		function map (data, source) {
			var queues = data.es.queue,
				prop,
				current,
				result = {},
				group,
				exists;
	        

	        for(prop in queues) {
	            current = queues[prop];
	            
	            if(current.groupName) {
	                
	                if(!result[current.groupName]) {
	                    group = createEmptyGroup(current.groupName);
	                } else {
	                    group = result[current.groupName];
	                }

	                exists = source[current.groupName];
	                group.show = exists ? exists.show : false;
	                group.queues.push(current);

	                group.length += current.length;
                    group.groupQueues += 1;
                    group.avgItemsPerSecond += current.avgItemsPerSecond;
                    group.totalItemsProcessed += current.totalItemsProcessed;
                    group.avgProcessingTime = (group.avgProcessingTime + current.avgProcessingTime) / group.groupQueues;
                    group.idleTimePercent = (group.idleTimePercent + current.idleTimePercent) / group.groupQueues;
                    group.lengthCurrentTryPeak = Math.max(group.lengthCurrentTryPeak, current.lengthCurrentTryPeak);
                    group.lengthLifetimePeak = Math.max(group.lengthLifetimePeak, current.lengthLifetimePeak);
                    
	                result[current.groupName] = group;
	            } else {
	                result[current.queueName] = current;
	            }
	        }
	        
	        return result;
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