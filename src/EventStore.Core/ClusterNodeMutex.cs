using System;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Core {
	public class ClusterNodeMutex {
		private static readonly ILogger Log = LogManager.GetLoggerFor<ClusterNodeMutex>();

		public readonly string MutexName;

		public bool IsAcquired {
			get { return _acquired; }
		}

		private Mutex _clusterNodeMutex;
		private bool _acquired;

		public ClusterNodeMutex() {
			MutexName = string.Format("ESCLUSTERNODE:{0}", Process.GetCurrentProcess().Id);
		}

		public bool Acquire() {
			if (_acquired)
				throw new InvalidOperationException(string.Format("Cluster Node mutex '{0}' is already acquired.",
					MutexName));

			try {
				_clusterNodeMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out _acquired);
			} catch (AbandonedMutexException exc) {
				Log.InfoException(exc,
					"Cluster Node mutex '{mutex}' is said to be abandoned. "
					+ "Probably previous instance of server was terminated abruptly.",
					MutexName);
			}

			return _acquired;
		}

		public void Release() {
			if (!_acquired)
				throw new InvalidOperationException(string.Format("Cluster Node mutex '{0}' was not acquired.",
					MutexName));
			_clusterNodeMutex.ReleaseMutex();
		}

		public static bool IsPresent(int pid) {
			var mutexName = string.Format("ESCLUSTERNODE:{0}", pid);
			try {
				using (Mutex.OpenExisting(mutexName, MutexRights.ReadPermissions)) {
					return true;
				}
			} catch (WaitHandleCannotBeOpenedException) {
				return false;
			} catch (Exception exc) {
				Log.TraceException(exc, "Exception while trying to open Cluster Node mutex '{mutex}': {e}.", mutexName,
					exc.Message);
			}

			return false;
		}
	}
}
