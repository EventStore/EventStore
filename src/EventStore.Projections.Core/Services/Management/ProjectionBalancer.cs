using System;

namespace EventStore.Projections.Core.Services.Management {
	class ProjectionBalancer : IProjectionBalancer {
		public void AddWorker(Guid workerID) {
			throw new NotImplementedException();
		}

		public void RemoveWorker(Guid workerId) {
			throw new NotImplementedException();
		}

		public void AddProjection(string projectionName) {
			throw new NotImplementedException();
		}

		public void RemoveProjection(string projectionName) {
			throw new NotImplementedException();
		}

		public void NotifyStarting(string projectionName, Guid coreId) {
			throw new NotImplementedException();
		}

		public void NotifyStarted(string projectionName, Guid coreId) {
			throw new NotImplementedException();
		}

		public void NotifyStopped(string projectionName, Guid coreId) {
			throw new NotImplementedException();
		}

		public void NotifyFaulted(string projectionName, Guid coreId) {
			throw new NotImplementedException();
		}
	}

	interface IProjectionBalancer {
		void AddWorker(Guid workerID);
		void RemoveWorker(Guid workerId);
		void AddProjection(string projectionName);
		void RemoveProjection(string projectionName);

		void NotifyStarting(string projectionName, Guid coreId);
		void NotifyStarted(string projectionName, Guid coreId);
		void NotifyStopped(string projectionName, Guid coreId);
		void NotifyFaulted(string projectionName, Guid coreId);
	}
}
