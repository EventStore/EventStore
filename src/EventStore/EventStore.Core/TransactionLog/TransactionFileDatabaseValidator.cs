// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.TransactionLog
{
    public class TransactionFileDatabaseValidator
    {
        private readonly TransactionFileDatabaseConfig _config;

        public TransactionFileDatabaseValidator(TransactionFileDatabaseConfig config)
        {
            Ensure.NotNull(config, "config");
            _config = config;
        }

        public void Validate()
        {
            var current = _config.WriterCheckpoint.Read();
            if (current < _config.SegmentSize)
            {
                VerifyFirstFile(current);
            }
            else
            {
                VerifyExistingFiles(current);
            }
            ValidateReaderChecksumsMustBeLess(_config.WriterCheckpoint, _config.Checkpoints);
        }

        private void ValidateReaderChecksumsMustBeLess(ICheckpoint writerCheckpoint, IEnumerable<ICheckpoint> readerCheckpoints)
        {
            var current = writerCheckpoint.Read();
            foreach (var checkpoint in readerCheckpoints)
            {
                if (checkpoint.Read() > current)
                    throw new CorruptDatabaseException(new ReaderCheckpointHigherThanWriterException(checkpoint.Name));
            }
        }

        private void VerifyFirstFile(long current)
        {
            var segmentName = _config.FileNamingStrategy.GetFilenameFor(0);
            if (File.Exists(segmentName))
            {
                VerifyFileSize(segmentName);
                EnsureNoOtherFiles(1);
            }
            else
            {
                if (current != 0)
                    throw new CorruptDatabaseException(new ChunkNotFoundException(segmentName));
                EnsureNoOtherFiles(0);

                using (var fileStream = new FileStream(segmentName, FileMode.Create, FileAccess.Write, FileShare.Read, 8096, FileOptions.SequentialScan))
                {
                    fileStream.SetLength(_config.SegmentSize);
                    fileStream.Close();
                }
            }
        }

        private void VerifyExistingFiles(long current)
        {
            //var expectedFiles = (int) (current / _config.segmentSize + (current % _config.segmentSize > 0 ? 1 : 0));
            var expectedFiles = (int)((current + _config.SegmentSize - 1) / _config.SegmentSize); // same as above
            for (int i = 0; i < expectedFiles; i++)
            {
                var segmentName = _config.FileNamingStrategy.GetFilenameFor(i);
                if (!File.Exists(segmentName))
                    throw new CorruptDatabaseException(new ChunkNotFoundException(segmentName));
                VerifyFileSize(segmentName);
            }
            EnsureNoOtherFiles(expectedFiles);
        }

        private void VerifyFileSize(string segmentName)
        {
            var info = new FileInfo(segmentName);
            if (info.Length != _config.SegmentSize)
            {
                throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                    string.Format("Chunk file '{0}' should have file size {1} bytes, but instead has {2} bytes length.",
                                    segmentName,
                                    _config.SegmentSize,
                                    info.Length)));
            }
        }

        private void EnsureNoOtherFiles(int expectedFiles)
        {
            var files = _config.FileNamingStrategy.GetAllPresentFiles();
            var actualFiles = files.Count();
            if (actualFiles != expectedFiles)
            {
                throw new CorruptDatabaseException(
                    new ExtraneousFileFoundException(string.Format("Expected file count: {0}, actual: {1}.",
                                                                   expectedFiles,
                                                                   actualFiles)));
            }
        }
    }

}