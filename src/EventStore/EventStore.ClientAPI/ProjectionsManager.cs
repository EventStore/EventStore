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

namespace EventStore.ClientAPI
{
    public class ProjectionsManager
    {
        private readonly ProjectionsClient _client;
        private readonly IPEndPoint _httpEndPoint;

        public ProjectionsManager(IPEndPoint httpEndPoint)
        {
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _client = new ProjectionsClient();
            _httpEndPoint = httpEndPoint;
        }

        public void Enable(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            EnableAsync(name).Wait();
        }

        public Task EnableAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Enable(_httpEndPoint, name);
        }

        public void Disable(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DisableAsync(name).Wait();
        }

        public Task DisableAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Disable(_httpEndPoint, name);
        }

        public void CreateOneTime(string query)
        {
            Ensure.NotNullOrEmpty(query, "query");
            CreateOneTimeAsync(query).Wait();
        }

        public Task CreateOneTimeAsync(string query)
        {
            Ensure.NotNullOrEmpty(query, "query");
            return _client.CreateOneTime(_httpEndPoint, query);
        }

        public void CreateContinuous(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            CreateContinuousAsync(name, query).Wait();
        }

        public Task CreateContinuousAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.CreateContinuous(_httpEndPoint, name, query);
        }

        public string ListAll()
        {
            return ListAllAsync().Result;
        }

        public Task<string> ListAllAsync()
        {
            return _client.ListAll(_httpEndPoint);
        }

        public string ListOneTime()
        {
            return ListOneTimeAsync().Result;
        }

        public Task<string> ListOneTimeAsync()
        {
            return _client.ListOneTime(_httpEndPoint);
        }

        public string ListContinuous()
        {
            return ListContinuousAsync().Result;
        }

        public Task<string> ListContinuousAsync()
        {
            return _client.ListContinuous(_httpEndPoint);
        }

        public string GetStatus(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatusAsync(name).Result;
        }

        public Task<string> GetStatusAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatus(_httpEndPoint, name);
        }

        public string GetState(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStateAsync(name).Result;
        }

        public Task<string> GetStateAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetState(_httpEndPoint, name);
        }

        public string GetStatistics(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatisticsAsync(name).Result;
        }

        public Task<string> GetStatisticsAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatistics(_httpEndPoint, name);
        }

        public string GetQuery(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetQueryAsync(name).Result;
        }

        public Task<string> GetQueryAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetQuery(_httpEndPoint, name);
        }

        public void UpdateQuery(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            UpdateQueryAsync(name, query).Wait();
        }

        public Task UpdateQueryAsync(string name, string query)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.UpdateQuery(_httpEndPoint, name, query);
        }

        public void Delete(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DeleteAsync(name).Wait();
        }

        public Task DeleteAsync(string name)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Delete(_httpEndPoint, name);
        }
    }
}
