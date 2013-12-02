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

namespace EventStore.ClientAPI.Exceptions
{
    /// <summary>
    /// Exception thrown if the number of retries for an operation is reached.
    /// To change the number of retries attempted for an operation, use the methods
    /// <see cref="ConnectionSettingsBuilder.LimitRetriesForOperationTo" /> or 
    /// <see cref="ConnectionSettingsBuilder.KeepRetrying" /> and pass the resulting
    /// <see cref="ConnectionSettings" /> into the constructor of the connection.
    /// </summary>
    public class RetriesLimitReachedException : EventStoreConnectionException
    {
        /// <summary>
        /// Constructs a new instance of <see cref="RetriesLimitReachedException"/>.
        /// </summary>
        /// <param name="retries">The number of retries attempted.</param>
        public RetriesLimitReachedException(int retries) 
            : base(string.Format("Reached retries limit : {0}", retries))
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="RetriesLimitReachedException"/>.
        /// </summary>
        /// <param name="item">The name of the item for which retries were attempted.</param>
        /// <param name="retries">The number of retries attempted.</param>
        public RetriesLimitReachedException(string item, int retries)
            : base(string.Format("Item {0} reached retries limit : {1}", item, retries))
        {
        }
    }
}
