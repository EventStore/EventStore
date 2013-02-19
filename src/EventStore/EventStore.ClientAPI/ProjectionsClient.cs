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
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI
{
    internal class ProjectionsClient
    {
        private readonly HttpAsyncClient _client = new HttpAsyncClient();

        public Task Enable(IPEndPoint endPoint, string name)
        {
            return SendPost(endPoint.ToHttpUrl("/projection/{0}/command/enable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task Disable(IPEndPoint endPoint, string name)
        {
            return SendPost(endPoint.ToHttpUrl("/projection/{0}/command/disable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task CreateOneTime(IPEndPoint endPoint, string query)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/onetime?type=JS"), query, HttpStatusCode.Created);
        }

        public Task CreateContinuous(IPEndPoint endPoint, string name, string query)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/continuous?name={0}&type=JS&emit=1", name), 
                            query, 
                            HttpStatusCode.Created);
        }

        public Task<string> ListAll(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/any"), HttpStatusCode.OK);
        }

        public Task<string> ListOneTime(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/onetime"), HttpStatusCode.OK);
        }

        public Task<string> ListContinuous(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/continuous"), HttpStatusCode.OK);
        }

        public Task<string> GetStatus(IPEndPoint endPoint, string name)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
        }

        public Task<string> GetState(IPEndPoint endPoint, string name)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/state", name), HttpStatusCode.OK);
        }

        public Task<string> GetStatistics(IPEndPoint endPoint, string name)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/statistics", name), HttpStatusCode.OK);
        }

        public Task<string> GetQuery(IPEndPoint endPoint, string name)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/query", name), HttpStatusCode.OK);
        }

        public Task UpdateQuery(IPEndPoint endPoint, string name, string query)
        {
            return SendPut(endPoint.ToHttpUrl("/projection/{0}/query?type=JS", name), query, HttpStatusCode.OK);
        }

        public Task Delete(IPEndPoint endPoint, string name)
        {
            return SendDelete(endPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
        }

        private Task<string> SendGet(string url, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Get(url,
                        response =>
                        {
                            if (response.HttpStatusCode == expectedCode)
                                source.SetResult(response.Body);
                            else
                                source.SetException(new ProjectionCommandFailedException(
                                                            string.Format("Server returned {0} ({1}) for GET on {2}",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription,
                                                                          url)));
                        },
                        source.SetException);

            return source.Task;
        }

        private Task<string> SendDelete(string url, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Delete(url,
                           response =>
                           {
                               if (response.HttpStatusCode == expectedCode)
                                   source.SetResult(response.Body);
                               else
                                   source.SetException(new ProjectionCommandFailedException(
                                                               string.Format("Server returned {0} ({1}) for DELETE on {2}",
                                                                             response.HttpStatusCode,
                                                                             response.StatusDescription,
                                                                             url)));
                           },
                           source.SetException);

            return source.Task;
        }

        private Task SendPut(string url, string content, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Put(url,
                        content,
                        ContentType.Json,
                        response =>
                        {
                            if (response.HttpStatusCode == expectedCode)
                                source.SetResult(null);
                            else
                                source.SetException(new ProjectionCommandFailedException(
                                                            string.Format("Server returned {0} ({1}) for PUT on {2}",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription,
                                                                          url)));
                        },
                        source.SetException);

            return source.Task;
        }

        private Task SendPost(string url, string content, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Post(url,
                         content,
                         ContentType.Json,
                         response =>
                         {
                             if (response.HttpStatusCode == expectedCode)
                                 source.SetResult(null);
                             else
                                 source.SetException(new ProjectionCommandFailedException(
                                                             string.Format("Server returned {0} ({1}) for POST on {2}",
                                                                           response.HttpStatusCode,
                                                                           response.StatusDescription,
                                                                           url)));
                         },
                         source.SetException);

            return source.Task;
        }
    }
}