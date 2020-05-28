using System;
using System.IO;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Util;
using ILogger = Serilog.ILogger;

namespace EventStore.Core {
	public class ExclusiveDbLock : IDisposable {
		private static readonly ILogger Log = Serilog.Log.ForContext<ExclusiveDbLock>();

		public readonly string MutexName;

		public bool IsAcquired {
			get { return _acquired; }
		}

		private Mutex _dbMutex;
		private bool _acquired;

		public ExclusiveDbLock(string dbPath) {
			Ensure.NotNullOrEmpty(dbPath, "dbPath");
			MutexName = @"Global\ESDB-HASHED:" + GetDbPathHash(dbPath);
		}

		public bool Acquire() {
			if (_acquired)
				throw new InvalidOperationException($"DB mutex '{MutexName}' is already acquired.");

			try {
				_dbMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out _acquired);
				_dbMutex.WaitOne(TimeSpan.FromSeconds(5));
			} catch (AbandonedMutexException exc) {
				Log.Warning(exc,
					"DB mutex '{mutex}' is said to be abandoned. "
					+ "Probably previous instance of server was terminated abruptly.",
					MutexName);
			}

			return _acquired;
		}

		private static string GetDbPathHash(string dbPath) {
			using var memStream = new MemoryStream(Helper.UTF8NoBom.GetBytes(dbPath));
			return BitConverter.ToString(MD5Hash.GetHashFor(memStream)).Replace("-", "");
		}

		public void Release() {
			if (!_acquired)
				throw new InvalidOperationException($"DB mutex '{MutexName}' was not acquired.");
			_dbMutex.ReleaseMutex();
		}

		public void Dispose() => _dbMutex?.Dispose();
	}
}
