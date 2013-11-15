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

using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// API for managing projections in the Event Store through C# code. Communicates
    /// with the Event Store over the RESTful API.
    /// </summary>
    public class ProjectionsManager
    {
        private readonly ProjectionsClient _client;
        private readonly IPEndPoint _httpEndPoint;

        /// <summary>
        /// Creates a new instance of <see cref="ProjectionsManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        public ProjectionsManager(ILogger log, IPEndPoint httpEndPoint)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _client = new ProjectionsClient(log);
            _httpEndPoint = httpEndPoint;
        }

        /// <summary>
        /// Synchronously enables a projection.
        /// </summary>
        /// <param name="name">The name of the projection</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a projection</param>
        public void Enable(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            EnableAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously enables a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task EnableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Enable(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously disables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        public void Disable(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DisableAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously disables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Disable(_httpEndPoint, name, userCredentials);
        }

        public void CreateOneTime(string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(query, "query");
            CreateOneTimeAsync(query, userCredentials).Wait();
        }

        public Task CreateOneTimeAsync(string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(query, "query");
            return _client.CreateOneTime(_httpEndPoint, query, userCredentials);
        }

        public void CreateContinuous(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            CreateContinuousAsync(name, query, userCredentials).Wait();
        }

        public Task CreateContinuousAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.CreateContinuous(_httpEndPoint, name, query, userCredentials);
        }

        public string ListAll(UserCredentials userCredentials = null)
        {
            return ListAllAsync(userCredentials).Result;
        }

        public Task<string> ListAllAsync(UserCredentials userCredentials = null)
        {
            return _client.ListAll(_httpEndPoint, userCredentials);
        }

        public string ListOneTime(UserCredentials userCredentials = null)
        {
            return ListOneTimeAsync(userCredentials).Result;
        }

        public Task<string> ListOneTimeAsync(UserCredentials userCredentials = null)
        {
            return _client.ListOneTime(_httpEndPoint, userCredentials);
        }

        public string ListContinuous(UserCredentials userCredentials = null)
        {
            return ListContinuousAsync(userCredentials).Result;
        }

        public Task<string> ListContinuousAsync(UserCredentials userCredentials = null)
        {
            return _client.ListContinuous(_httpEndPoint, userCredentials);
        }

        public string GetStatus(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatusAsync(name, userCredentials).Result;
        }

        public Task<string> GetStatusAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatus(_httpEndPoint, name, userCredentials);
        }

        public string GetState(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStateAsync(name, userCredentials).Result;
        }

        public Task<string> GetStateAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetState(_httpEndPoint, name, userCredentials);
        }

        public string GetStatistics(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatisticsAsync(name, userCredentials).Result;
        }

        public Task<string> GetStatisticsAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatistics(_httpEndPoint, name, userCredentials);
        }

        public string GetQuery(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetQueryAsync(name, userCredentials).Result;
        }

        public Task<string> GetQueryAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetQuery(_httpEndPoint, name, userCredentials);
        }

        public void UpdateQuery(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            UpdateQueryAsync(name, query, userCredentials).Wait();
        }

        public Task UpdateQueryAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.UpdateQuery(_httpEndPoint, name, query, userCredentials);
        }

        /// <summary>
        /// Synchronously deletes a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        public void Delete(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DeleteAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously deletes a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Delete(_httpEndPoint, name, userCredentials);
        }
    }
}
