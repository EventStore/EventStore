using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI.Connection
{
    internal class ProjectionsManager
    {
        private readonly IPEndPoint _httpEndPoint;
        private readonly HttpAsyncClient _client = new HttpAsyncClient();

        public ProjectionsManager(IPEndPoint httpEndPoint)
        {
            _httpEndPoint = httpEndPoint;
        }

        public Task Enable(string name)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projection/{0}/command/enable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task Disable(string name)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projection/{0}/command/disable", name), string.Empty, HttpStatusCode.OK);
        }

        public Task CreateOneTime(string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/onetime?type=JS"), query, HttpStatusCode.Created);
        }

        public Task CreateAdHoc(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/adhoc?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreateContinious(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/continuous?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task CreatePersistent(string name, string query)
        {
            return SendPost(_httpEndPoint.ToHttpUrl("/projections/persistent?name={0}&type=JS", name), query, HttpStatusCode.Created);
        }

        public Task<string> ListAll()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/any"), HttpStatusCode.OK);
        }

        public Task<string> ListOneTime()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/onetime"), HttpStatusCode.OK);
        }

        public Task<string> ListAdHoc()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/adhoc"), HttpStatusCode.OK);
        }

        public Task<string> ListContinuous()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/continuous"), HttpStatusCode.OK);
        }

        public Task<string> ListPersistent()
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projections/persistent"), HttpStatusCode.OK);
        }

        public Task<string> GetStatus(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
        }

        public Task<string> GetState(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/state", name), HttpStatusCode.OK);
        }

        public Task<string> GetStatistics(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/statistics", name), HttpStatusCode.OK);
        }

        public Task<string> GetQuery(string name)
        {
            return SendGet(_httpEndPoint.ToHttpUrl("/projection/{0}/query", name), HttpStatusCode.OK);
        }

        public Task UpdateQuery(string name, string query)
        {
            return SendPut(_httpEndPoint.ToHttpUrl("/projection/{0}/query?type=JS", name), query, HttpStatusCode.OK);
        }

        public Task Delete(string name)
        {
            return SendDelete(_httpEndPoint.ToHttpUrl("/projection/{0}", name), HttpStatusCode.OK);
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