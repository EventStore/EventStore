using System;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage
{
    public class StorageScavenger: IHandle<SystemMessage.ScavengeDatabase>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly IReadIndex _readIndex;

        private readonly object _scavengingLock = new object();
        private bool _isScavengingRunning;

        public StorageScavenger(TFChunkDb db, IReadIndex readIndex)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(readIndex, "readIndex");
            _db = db;
            _readIndex = readIndex;
        }

        public void Handle(SystemMessage.ScavengeDatabase message)
        {
            lock (_scavengingLock)
            {
                if (_isScavengingRunning)
                    return;
                _isScavengingRunning = true;
            }
            ThreadPool.QueueUserWorkItem(_ => Scavenge());
        }

        private void Scavenge()
        {
            try
            {
                var scavenger = new TFChunkScavenger(_db, _readIndex);
                scavenger.Scavenge(alwaysKeepScavenged: true);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error while scavenging DB.");
            }

            lock (_scavengingLock)
            {
                _isScavengingRunning = false;
            }
        }
    }
}