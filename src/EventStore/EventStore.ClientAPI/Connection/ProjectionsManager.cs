using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI.Connection
{
    internal class ProjectionsManager
    {
        private readonly HttpAsyncClient _client = new HttpAsyncClient();

        public ProjectionsManager()
        {
        }

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

        public Task CreateAdHoc(IPEndPoint endPoint, string name, string query)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/adhoc?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreateContinious(IPEndPoint endPoint, string name, string query)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/continuous?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreatePersistent(IPEndPoint endPoint, string name, string query)
        {
            return SendPost(endPoint.ToHttpUrl("/projections/persistent?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task<string> ListAll(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/any"), HttpStatusCode.OK);
        }

        public Task<string> ListOneTime(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/onetime"), HttpStatusCode.OK);
        }

        public Task<string> ListAdHoc(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/adhoc"), HttpStatusCode.OK);
        }

        public Task<string> ListContinuous(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/continuous"), HttpStatusCode.OK);
        }

        public Task<string> ListPersistent(IPEndPoint endPoint)
        {
            return SendGet(endPoint.ToHttpUrl("/projections/persistent"), HttpStatusCode.OK);
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
                                                            string.Format("Server returned : {0} ({1})",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription)));
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
                                                               string.Format("Server returned : {0} ({1})",
                                                                             response.HttpStatusCode,
                                                                             response.StatusDescription)));
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
                                                            string.Format("Server returned : {0} ({1})",
                                                                          response.HttpStatusCode,
                                                                          response.StatusDescription)));
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
                                                             string.Format("Server returned : {0} ({1})",
                                                                           response.HttpStatusCode,
                                                                           response.StatusDescription)));
                         },
                         source.SetException);

            return source.Task;
        }
    }
}