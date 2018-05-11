using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Index;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Services.Storage
{

    

    public class StorageScavenger : IHandle<ClientMessage.ScavengeDatabase>, IHandle<UserManagementMessage.UserManagementServiceInitialized>
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<StorageScavenger>();

        private readonly TFChunkDb _db;
        private readonly ITableIndex _tableIndex;
        private readonly IReadIndex _readIndex;
        private readonly bool _alwaysKeepScavenged;
        private readonly bool _mergeChunks;
        private readonly bool _unsafeIgnoreHardDeletes;
        private readonly ITFChunkScavengerLogManager _logManager;
        private int _isScavengingRunning;

        public StorageScavenger(TFChunkDb db, ITableIndex tableIndex, IReadIndex readIndex, ITFChunkScavengerLogManager logManager, bool alwaysKeepScavenged, bool mergeChunks, bool unsafeIgnoreHardDeletes)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(logManager, "logManager");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(readIndex, "readIndex");

            _db = db;
            _tableIndex = tableIndex;
            _readIndex = readIndex;
            _alwaysKeepScavenged = alwaysKeepScavenged;
            _mergeChunks = mergeChunks;            
            _unsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
            _logManager = logManager;
        }

        public void Handle(UserManagementMessage.UserManagementServiceInitialized message)
        {
            _logManager.Initialise();
        }

        public void Handle(ClientMessage.ScavengeDatabase message)
        {
            if (message.User == null || (!message.User.IsInRole(SystemRoles.Admins) && !message.User.IsInRole(SystemRoles.Operations)))
            {
                message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
                    ClientMessage.ScavengeDatabaseResponse.ScavengeResponse.Unauthorized, null));
            }
            else if (Interlocked.CompareExchange(ref _isScavengingRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => Scavenge(message));
            }
            else
            {
                message.Envelope.ReplyWith(new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId,
                    ClientMessage.ScavengeDatabaseResponse.ScavengeResponse.InProgress, null));
            }
        }

        private void Scavenge(ClientMessage.ScavengeDatabase message)
        {
            var sw = Stopwatch.StartNew();

            var tfChunkScavengerLog = _logManager.CreateLog();

            ScavengeResult result = ScavengeResult.Success;
            string error = null;
            long spaceSaved = 0;
            try
            {
                tfChunkScavengerLog.ScavengeStarted();

                message.Envelope.ReplyWith(
                    new ClientMessage.ScavengeDatabaseResponse(message.CorrelationId, ClientMessage.ScavengeDatabaseResponse.ScavengeResponse.Started, tfChunkScavengerLog.ScavengeId)
                );
                
                var scavenger = new TFChunkScavenger(_db, tfChunkScavengerLog, _tableIndex, _readIndex, unsafeIgnoreHardDeletes: _unsafeIgnoreHardDeletes);

                spaceSaved = scavenger.Scavenge(_alwaysKeepScavenged, _mergeChunks);
                
            }
            catch (Exception exc)
            {
                result = ScavengeResult.Failed;
                Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
                error = string.Format("Error while scavenging DB: {0}.", exc.Message);
            }

            Interlocked.Exchange(ref _isScavengingRunning, 0);

            tfChunkScavengerLog.ScavengeCompleted(result, error, spaceSaved, sw.Elapsed);
        }

    }
}
