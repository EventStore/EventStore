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

using System.Threading.Tasks;

namespace EventStore.ClientAPI
{
    public interface IProjectionsManagement
    {
        void Enable(string name);
        Task EnableAsync(string name);

        void Disable(string name);
        Task DisableAsync(string name);

        void CreateOneTime(string query);
        Task CreateOneTimeAsync(string query);

        void CreateAdHoc(string name, string query);
        Task CreateAdHocAsync(string name, string query);

        void CreateContinuous(string name, string query);
        Task CreateContinuousAsync(string name, string query);

        void CreatePersistent(string name, string query);
        Task CreatePersistentAsync(string name, string query);

        string ListAll();
        Task<string> ListAllAsync();

        string ListOneTime();
        Task<string> ListOneTimeAsync();

        string ListAdHoc();
        Task<string> ListAdHocAsync();

        string ListContinious();
        Task<string> ListContiniousAsync();

        string ListPersistent();
        Task<string> ListPersistentAsync();

        string GetStatus(string name);
        Task<string> GetStatusAsync(string name);

        string GetState(string name);
        Task<string> GetStateAsync(string name);

        string GetStatistics(string name);
        Task<string> GetStatisticsAsync(string name);

        string GetQuery(string name);
        Task<string> GetQueryAsync(string name);

        void UpdateQuery(string name, string query);
        Task UpdateQueryAsync(string name, string query);

        void Delete(string name);
        Task DeleteAsync(string name);
    }
}
