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
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Core;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a multi-request transaction with the Event Store
    /// </summary>
    public class EventStoreTransaction : IDisposable
    {
        public readonly long TransactionId;

        private readonly UserCredentials _userCredentials;
        private readonly IEventStoreTransactionConnection _connection;
        private bool _isRolledBack;
        private bool _isCommitted;

        /// <summary>
        /// Constructs a new <see cref="EventStoreTransaction"/>
        /// </summary>
        /// <param name="transactionId">The transaction id of the transaction</param>
        /// <param name="userCredentials">User credentials under which transaction is committed.</param>
        /// <param name="connection">The connection the transaction is hooked to</param>
        internal EventStoreTransaction(long transactionId, UserCredentials userCredentials, IEventStoreTransactionConnection connection)
        {
            Ensure.Nonnegative(transactionId, "transactionId");

            TransactionId = transactionId;
            _userCredentials = userCredentials;
            _connection = connection;
        }

        /// <summary>
        /// Commits this transaction
        /// </summary>
        /// <returns>Expected version for following write requests</returns>
        public WriteResult Commit()
        {
            return CommitAsync().Result;
        }

        /// <summary>
        /// Asynchronously commits this transaction
        /// </summary>
        /// <returns>A <see cref="Task"/> that returns expected version for following write requests</returns>
        public Task<WriteResult> CommitAsync()
        {
            if (_isRolledBack) throw new InvalidOperationException("Can't commit a rolledback transaction");
            if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
            _isCommitted = true;
            return _connection.CommitTransactionAsync(this, _userCredentials);
        }

        /// <summary>
        /// Writes to a transaction in the event store asynchronously
        /// </summary>
        /// <param name="events">The events to write</param>
        public void Write(IEnumerable<EventData> events)
        {
            WriteAsync(events).Wait();
        }

        /// <summary>
        /// Writes to a transaction in the event store asynchronously
        /// </summary>
        /// <param name="events">The events to write</param>
        public void Write(params EventData[] events)
        {
            WriteAsync((IEnumerable<EventData>)events).Wait();
        }

        /// <summary>
        /// Writes to a transaction in the event store asynchronously
        /// </summary>
        /// <param name="events">The events to write</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        public Task WriteAsync(params EventData[] events)
        {
            return WriteAsync((IEnumerable<EventData>)events);
        }

        /// <summary>
        /// Writes to a transaction in the event store asynchronously
        /// </summary>
        /// <param name="events">The events to write</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        public Task WriteAsync(IEnumerable<EventData> events)
        {
            if (_isRolledBack) throw new InvalidOperationException("can't write to a rolledback transaction");
            if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
            return _connection.TransactionalWriteAsync(this, events);
        }

        /// <summary>
        /// Rollsback this transaction.
        /// </summary>
        public void Rollback()
        {
            if (_isCommitted) throw new InvalidOperationException("Transaction is already committed");
            _isRolledBack = true;
        } 

        /// <summary>
        /// Disposes this transaction rolling it back if not already committed
        /// </summary>
        public void Dispose()
        {
            if (!_isCommitted)
                _isRolledBack = true;
        }
    }
}
