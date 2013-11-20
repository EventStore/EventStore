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
using System;
using System.IO;
using System.Text;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Core
{
    public class ExclusiveDbLock
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ExclusiveDbLock>();

        public readonly string MutexName;
        public bool IsAcquired { get { return _acquired; } }

        private Mutex _dbMutex;
        private bool _acquired;

        public ExclusiveDbLock(string dbPath)
        {
            Ensure.NotNullOrEmpty(dbPath, "dbPath");
            MutexName = dbPath.Length <= 250 ? "ESDB:" + dbPath.Replace('\\', '/') : "ESDB-HASHED:" + GetDbPathHash(dbPath);
            MutexName += new string('-', 260 - MutexName.Length);
        }

        public bool Acquire()
        {
            if (_acquired) throw new InvalidOperationException(string.Format("DB mutex '{0}' is already acquired.", MutexName));

            try
            {
                _dbMutex = new Mutex(initiallyOwned: true, name: MutexName, createdNew: out _acquired);
            }
            catch (AbandonedMutexException exc)
            {
                Log.InfoException(exc, 
                                  "DB mutex '{0}' is said to be abandoned. " 
                                  + "Probably previous instance of server was terminated abruptly.", 
                                  MutexName);
            }

            return _acquired;
        }

        private string GetDbPathHash(string dbPath)
        {
            using (var memStream = new MemoryStream(Helper.UTF8NoBom.GetBytes(dbPath)))
            {
                return BitConverter.ToString(MD5Hash.GetHashFor(memStream)).Replace("-", "");
            }
        }

        public void Release()
        {
            if (!_acquired) throw new InvalidOperationException(string.Format("DB mutex '{0}' was not acquired.", MutexName));
            _dbMutex.ReleaseMutex();
        }
    }
}