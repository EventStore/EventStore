if (!window.es) { window.es = {}; };
window.es.statsBinding = function() {

	var stats = [
		new es.TimeSeries({
			title: "Machine CPU %",
			getData: function(data) {
				return data.sys.cpu;
			}
		}),
		new es.TimeSeries({
			title: "Process VM",
			getData: function(data) {
				return data.proc.mem;
			}
		}),
		new es.TimeSeries({
			title: "Machine Available VM",
			getData: function(data) {
				return data.sys.freeMem;
			}
		}),
		new es.TimeSeries({
			title: "Tcp Sending speed",
			getData: function(data) {
				return data.proc.tcp.sendingSpeed;
			}
		}),
		new es.TimeSeries({
			title: "Tcp Receiving speed",
			getData: function(data) {
				return data.proc.tcp.receivingSpeed;
			}
		}),
		new es.TimeSeries({
		    title: "Read Index Failed Read Count",
			getData: function(data) {
			    return data.es.readIndex.failedReadCount;
			}
		}),
		new es.TimeSeries({
		    title: "Read Index Succ Read Count",
			getData: function(data) {
			    return data.es.readIndex.succReadCount;
			}
		}),
		new es.TimeSeries({
			title: "Disk Read Bytes",
			getData: function(data) {
				return data.proc.diskIo.readBytes;
			}
		}),
		new es.TimeSeries({
			title: "Disk Written Bytes",
			getData: function(data) {
				return data.proc.diskIo.writtenBytes;
			}
		})
	];
	
	return stats;
};

window.es.statsBindingQueue = function () {

    var stats = [
	    new es.TimeSeries({
	        title: "Main Queue Avg",
	        getData: function (data) {
	            return data.es.queue.mainQueue.avgItemsPerSecond;
	        }
	    }),
	    new es.TimeSeries({
	        title: "Storage Writer Queue Avg",
	        getData: function (data) {
	            return data.es.queue.storageWriterQueue.avgItemsPerSecond;
	        }
	    }),
	    new es.TimeSeries({
	        title: "Storage Reader Queue #0 Avg",
	        getData: function (data) {
	            return data.es.queue["storageReaderQueue #0"].avgItemsPerSecond;
	        }
	    }),
	    new es.TimeSeries({
	        title: "Projection Core Queue Avg",
	        getData: function (data) {
	            return data.es.queue.projectionCoreQueue.avgItemsPerSecond;
	        }
	    }),
	    new es.TimeSeries({
            title: "Monitoring Queue Avg",
            getData: function (data) {
                return data.es.queue.monitoringQueue.avgItemsPerSecond;
            }
        })
	];

    return stats;
};