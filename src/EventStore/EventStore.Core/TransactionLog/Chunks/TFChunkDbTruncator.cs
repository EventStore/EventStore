using System;
using System.IO;
using EventStore.Common.Log;
using EventStore.Common.Utils;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkDbTruncator
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkDbTruncator>();

        private readonly TFChunkDbConfig _config;

        public TFChunkDbTruncator(TFChunkDbConfig config)
        {
            Ensure.NotNull(config, "config");
            _config = config;
        }

        public void TruncateDb(long truncateChk)
        {
            var writerChk = _config.WriterCheckpoint.Read();
            var oldLastChunkNum = (int)(writerChk / _config.ChunkSize);
            var newLastChunkNum = (int)(truncateChk / _config.ChunkSize);

            var excessiveChunks = _config.FileNamingStrategy.GetAllVersionsFor(oldLastChunkNum + 1);
            if (excessiveChunks.Length > 0)
                throw new Exception(string.Format("During truncation of DB excessive TFChunks were found:\n{0}.", string.Join("\n", excessiveChunks)));

            // we need to remove excessive chunks from largest number to lowest one, so in case of crash
            // mid-process, we don't end up with broken non-sequential chunks sequence.
            for (int i=oldLastChunkNum; i > newLastChunkNum; i -= 1)
            {
                foreach (var chunkFile in _config.FileNamingStrategy.GetAllVersionsFor(i))
                {
                    Log.Info("File {0} will be deleted during TruncateDb procedure.", chunkFile);
                    File.SetAttributes(chunkFile, FileAttributes.Normal);
                    File.Delete(chunkFile);
                }
            }

            // it's not bad if there is no file, it could have been deleted on previous run
            var truncChunkFiles = _config.FileNamingStrategy.GetAllVersionsFor(newLastChunkNum);
            if (truncChunkFiles.Length > 0) 
            {
                // if the chunk we want to truncate into is already scavenged 
                // we have to truncate (i.e., delete) the whole chunk, not just part of it
                ChunkHeader chunkHeader;
                using (var fs = File.OpenRead(truncChunkFiles[0]))
                {
                    chunkHeader = ChunkHeader.FromStream(fs);
                }
                if (chunkHeader.IsScavenged)
                {
                    truncateChk = chunkHeader.ChunkStartPosition;
                    Log.Info("Setting TruncateCheckpoint to {0} and deleting WHOLE chunk(s) {1} as truncation position is in the middle of scavenged chunk.",
                             truncateChk, string.Join(", ", truncChunkFiles));
                    foreach (var chunkFile in truncChunkFiles)
                    {
                        File.SetAttributes(chunkFile, FileAttributes.Normal);
                        File.Delete(chunkFile);
                    }
                }
            }

            if (_config.EpochCheckpoint.Read() >= truncateChk)
            {
                Log.Info("Truncating epoch from {0} (0x{0:X}) to {1} (0x{1:X}).", _config.EpochCheckpoint.Read(), -1);
                _config.EpochCheckpoint.Write(-1);
                _config.EpochCheckpoint.Flush();
            }

            if (_config.ChaserCheckpoint.Read() > truncateChk)
            {
                Log.Info("Truncating chaser from {0} (0x{0:X}) to {1} (0x{1:X}).", _config.ChaserCheckpoint.Read(), truncateChk);
                _config.ChaserCheckpoint.Write(truncateChk);
                _config.ChaserCheckpoint.Flush();
            }

            if (_config.WriterCheckpoint.Read() > truncateChk)
            {
                Log.Info("Truncating writer from {0} (0x{0:X}) to {1} (0x{1:X}).", _config.WriterCheckpoint.Read(), truncateChk);
                _config.WriterCheckpoint.Write(truncateChk);
                _config.WriterCheckpoint.Flush();
            }

            Log.Info("Resetting TruncateCheckpoint to {0} (0x{0:X}).", -1);
            _config.TruncateCheckpoint.Write(-1);
            _config.TruncateCheckpoint.Flush();
        }
    }
}
