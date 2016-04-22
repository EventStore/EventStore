using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using Newtonsoft.Json.Linq;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI.Projections
{
    internal class ProjectionsClient
    {
        private readonly HttpAsyncClient _client;
        private readonly TimeSpan _operationTimeout;

        public ProjectionsClient(ILogger log, TimeSpan operationTimeout)
        {
            _operationTimeout = operationTimeout;
            _client = new HttpAsyncClient(_operationTimeout);
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

        [Obsolete("Use 'Task<List<ProjectionDetails>> ListAll' instead")]
        public Task<string> ListAllAsString(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/any"), userCredentials, HttpStatusCode.OK);
        }

        public Task<List<ProjectionDetails>> ListAll(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/any"), userCredentials, HttpStatusCode.OK)
                    .ContinueWith(x =>
                    {
                        if (x.IsFaulted) throw x.Exception;
                        var r = JObject.Parse(x.Result);
                        return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
                    });
        }

        [Obsolete("Use 'Task<List<ProjectionDetails>> ListOneTime' instead")]
        public Task<string> ListOneTimeAsString(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/onetime"), userCredentials, HttpStatusCode.OK);
        }

        public Task<List<ProjectionDetails>> ListOneTime(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/onetime"), userCredentials, HttpStatusCode.OK)
                    .ContinueWith(x =>
                    {
                        if (x.IsFaulted) throw x.Exception;
                        var r = JObject.Parse(x.Result);
                        return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
                    });
        }

        [Obsolete("Use 'Task<List<ProjectionDetails>> ListContinuous' instead")]
        public Task<string> ListContinuousAsString(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/continuous"), userCredentials, HttpStatusCode.OK);
        }

        public Task<List<ProjectionDetails>> ListContinuous(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/continuous"), userCredentials, HttpStatusCode.OK)
                    .ContinueWith(x =>
                    {
                        if (x.IsFaulted) throw x.Exception;
                        var r = JObject.Parse(x.Result);
                        return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
                    });
        }

        public Task<string> GetStatus(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetState(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/state", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetPartitionStateAsync(IPEndPoint endPoint, string name, string partition, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/state?partition={1}", name, partition), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetResult(IPEndPoint endPoint, string name, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/result", name), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetPartitionResultAsync(IPEndPoint endPoint, string name, string partition, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/projection/{0}/result?partition={1}", name, partition), userCredentials, HttpStatusCode.OK);
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
                        response =>
                        {
                            if (response.HttpStatusCode == expectedCode)
                                source.SetResult(response.Body);
                            else
                                source.SetException(new ProjectionCommandFailedException(
                                                            response.HttpStatusCode,
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
                           response =>
                           {
                               if (response.HttpStatusCode == expectedCode)
                                   source.SetResult(response.Body);
                               else
                                   source.SetException(new ProjectionCommandFailedException(
                                                               response.HttpStatusCode,
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
                        response =>
                        {
                            if (response.HttpStatusCode == expectedCode)
                                source.SetResult(null);
                            else
                                source.SetException(new ProjectionCommandFailedException(
                                                            response.HttpStatusCode,
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
                         userCredentials,
                         response =>
                         {
                             if (response.HttpStatusCode == expectedCode)
                                 source.SetResult(null);
                             else if (response.HttpStatusCode == 409)
                                 source.SetException(new ProjectionCommandConflictException(response.HttpStatusCode, response.StatusDescription));
                             else
                                 source.SetException(new ProjectionCommandFailedException(
                                                             response.HttpStatusCode,
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