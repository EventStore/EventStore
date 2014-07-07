using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI
{
    internal class ProjectionsClient
    {
        private readonly HttpAsyncClient _client;
        private readonly TimeSpan _operationTimeout;

        public ProjectionsClient(ILogger log, TimeSpan operationTimeout)
        {
            _operationTimeout = operationTimeout;
            _client = new HttpAsyncClient(log);
        }

        public Task Enable(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/projection/{0}/command/enable", name), string.Empty, userCredentials, HttpStatusCode.OK);
        }

        public Task Disable(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/projection/{0}/command/disable", name), string.Empty, userCredentials, HttpStatusCode.OK);
        }

        public Task Abort(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/projection/{0}/command/abort", name), string.Empty, userCredentials, HttpStatusCode.OK);
        }

        public Task CreateOneTime(IPEndPoint endPoint, string query, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/onetime?type=JS"), query, userCredentials, HttpStatusCode.Created);
        }

        public Task CreateTransient(IPEndPoint endPoint, string name, string query, UserCredentials userCredentials = null)
        {
            return SendPost(
                endPoint.ToHttpUrl("/projections/transient?name={0}&type=JS", name),
                query,
                userCredentials,
                HttpStatusCode.Created);
        }

        public Task CreateContinuous(IPEndPoint endPoint, string name, string query, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/continuous?name={0}&type=JS&emit=1", name), 
                            query, userCredentials, HttpStatusCode.Created);
        }

        public Task<string> ListAll(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/any"), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> ListOneTime(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/onetime"), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> ListContinuous(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/continuous"), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetStatus(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetState(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/state", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetStatistics(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/statistics", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetQuery(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/query", name), userCredentials, HttpStatusCode.OK);
        }

        public Task UpdateQuery(IPEndPoint endPoint, string name, string query, UserCredentials userCredentials = null)
        {
            return SendPut(endPoint.ToHttpUrl("/projection/{0}/query?type=JS", name), query, userCredentials, HttpStatusCode.OK);
        }

        public Task Delete(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendDelete(endPoint.ToHttpUrl("/projection/{0}", name), userCredentials, HttpStatusCode.OK);
        }

        private Task<string> SendGet(string url, UserCredentials userCredentials, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Get(url,
                        userCredentials,
                        _operationTimeout,
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

        private Task<string> SendDelete(string url, UserCredentials userCredentials, int expectedCode)
        {
            var source = new TaskCompletionSource<string>();
            _client.Delete(url,
                           userCredentials,
                           _operationTimeout,
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

        private Task SendPut(string url, string content, UserCredentials userCredentials, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Put(url,
                        content,
                        "application/json",
                        userCredentials,
                        _operationTimeout,
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

        private Task SendPost(string url, string content, UserCredentials userCredentials, int expectedCode)
        {
            var source = new TaskCompletionSource<object>();
            _client.Post(url,
                         content,
                         "application/json",
                         _operationTimeout,
                         userCredentials,
                         response =>
                         {
                             if (response.HttpStatusCode == expectedCode)
                                 source.SetResult(null);
                             else if (response.HttpStatusCode == 409)
                                 source.SetException(new ProjectionCommandConflictException(response.StatusDescription));
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