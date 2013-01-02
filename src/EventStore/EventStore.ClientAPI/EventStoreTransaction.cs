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
using System.IO;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a multi-request transaction with the Event Store
    /// </summary>
    public class EventStoreTransaction : IDisposable
    {
        internal readonly string Stream;
        internal readonly long TransactionId;
        private readonly EventStoreConnection _connection;
        private bool isRolledBack = false;
        private bool isCommitted = false;

        /// <summary>
        /// Commits this transaction
        /// </summary>
        public void Commit()
        {
            if(isRolledBack) throw new InvalidOperationException("can't commit a rolledback transaction");
            if(isCommitted) throw new InvalidOperationException("Transaction is already committed");
            _connection.CommitTransaction(this);
            isCommitted = true;
        }

        /// <summary>
        /// Asynchronously commits this transaction
        /// </summary>
        /// <returns>A <see cref="Task"/> the caller can use to control the async operation</returns>
        public Task CommitAsync()
        {
            if (isRolledBack) throw new InvalidOperationException("can't commit a rolledback transaction");
            if (isCommitted) throw new InvalidOperationException("transaction is already committed");
            isCommitted = true;
            return _connection.CommitTransactionAsync(this);
        }


        /// <summary>
        /// Starts a transaction in the event store on a given stream synchronously
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="events">The events to write</param>
        public void Write(IEnumerable<IEvent> events)
        {
            if (isRolledBack) throw new InvalidOperationException("can't write to a rolledback transaction");
            if (isCommitted) throw new InvalidOperationException("Transaction is already committed");
            _connection.TransactionalWrite(this, events);
        }

        /// <summary>
        /// Starts a transaction in the event store on a given stream asynchronously
        /// </summary>
        /// <remarks>
        /// A <see cref="EventStoreTransaction"/> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and the event store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="events">The events to write</param>
        /// <returns>A <see cref="Task"/> allowing the caller to control the async operation</returns>
        public Task WriteAsync(IEnumerable<IEvent> events)
        {
            if (isRolledBack) throw new InvalidOperationException("can't write to a rolledback transaction");
            if (isCommitted) throw new InvalidOperationException("Transaction is already committed");
            return _connection.TransactionalWriteAsync(this, events);
        }

        /// <summary>
        /// Rollsback this transaction.
        /// </summary>
        public void Rollback()
        {
            if (isCommitted) throw new InvalidOperationException("Transaction is already committed");
            isRolledBack = true;
        } 

        /// <summary>
        /// Constucts a new <see cref="EventStoreTransaction"/>
        /// </summary>
        /// <param name="stream">The stream in the transaction</param>
        /// <param name="transactionId">The transaction id of the transaction</param>
        /// <param name="connection">The connection the transaction is hooked to</param>
        internal EventStoreTransaction(string stream, long transactionId, EventStoreConnection connection)
        {
            Ensure.NotNullOrEmpty(stream, "stream");
            Stream = stream;
            TransactionId = transactionId;
            _connection = connection;
        }

        /// <summary>
        /// Disposes this transaction rolling it back if not already committed
        /// </summary>
        public void Dispose()
        {
            if(!isCommitted)
                isRolledBack = true;
        }
    }
}
