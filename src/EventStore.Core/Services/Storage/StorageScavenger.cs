using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage
{
    public class StorageScavenger: IHandle<ClientMessage.ScavengeDatabase>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly IReadIndex _readIndex;
        private readonly bool _alwaysKeepScavenged;
        private readonly bool _mergeChunks;

        private int _isScavengingRunning;

        public StorageScavenger(TFChunkDb db, ITableIndex tableIndex, IHasher hasher, 
                                IReadIndex readIndex, bool alwaysKeepScavenged, bool mergeChunks)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            Ensure.NotNull(readIndex, "readIndex");

            _db = db;
            _tableIndex = tableIndex;
            _hasher = hasher;
            _readIndex = readIndex;
            _alwaysKeepScavenged = alwaysKeepScavenged;
            _mergeChunks = mergeChunks;
        }

        public void Handle(ClientMessage.ScavengeDatabase message)
        {
            if (Interlocked.CompareExchange(ref _isScavengingRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Scavenge(message));
            }
            else 
            {
                message.Envelope.ReplyWith(
                    new ClientMessage.ScavengeDatabaseCompleted(message.CorrelationId, 
                                                                ClientMessage.ScavengeDatabase.ScavengeResult.InProgress,
                                                                "Scavenge already in progress.", 
                                                                TimeSpan.FromMilliseconds(0),
                                                                0)
                    );
            }
        }

        private void Scavenge(ClientMessage.ScavengeDatabase message)
        {
            var sw = Stopwatch.StartNew();
            ClientMessage.ScavengeDatabase.ScavengeResult result;
            string error = null;
            long spaceSaved = 0;
            try
            {
                if (message.User == null || (!message.User.IsInRole(SystemRoles.Admins) && !message.User.IsInRole(SystemRoles.Operations)))
                {
                    result = ClientMessage.ScavengeDatabase.ScavengeResult.Failed;
                    error = "Access denied.";
                }
                else
                {
                    var scavenger = new TFChunkScavenger(_db, _tableIndex, _hasher, _readIndex);
                    spaceSaved = scavenger.Scavenge(_alwaysKeepScavenged, _mergeChunks);
                    result = ClientMessage.ScavengeDatabase.ScavengeResult.Success;
                }
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
                result = ClientMessage.ScavengeDatabase.ScavengeResult.Failed;
                error = string.Format("Error while scavenging DB: {0}.", exc.Message);
            }

            Interlocked.Exchange(ref _isScavengingRunning, 0);

            message.Envelope.ReplyWith(
                new ClientMessage.ScavengeDatabaseCompleted(message.CorrelationId, result, error, sw.Elapsed, spaceSaved));
        }
    }
}