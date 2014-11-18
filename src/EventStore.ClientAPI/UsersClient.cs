using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.ClientAPI
{
    internal class UsersClient
    {
        private readonly HttpAsyncClient _client;

        private readonly TimeSpan _operationTimeout;

        public UsersClient(ILogger log, TimeSpan operationTimeout)
        {
            _operationTimeout = operationTimeout;
            _client = new HttpAsyncClient(log);
        }

public Task Enable(IPEndPoint endPoint, string login, UserCredentials userCredentials)
        {
            return SendPost(endPoint.ToHttpUrl("/users/{login}/command/enable", login), string.Empty, userCredentials, HttpStatusCode.OK);
    }

public  Task Disable(IPEndPoint endPoint, string login, UserCredentials userCredentials)
        {
            return SendPost(endPoint.ToHttpUrl("/users/{login}/command/disable", login), string.Empty, userCredentials, HttpStatusCode.OK);
        }

        public Task Delete(IPEndPoint endPoint, string login, UserCredentials userCredentials)
{
    return SendDelete(endPoint.ToHttpUrl("/users/{login}", login), userCredentials, HttpStatusCode.OK);
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
                        source.SetException(new UserCommandFailedException(
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
                           source.SetException(new UserCommandFailedException(
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
                        source.SetException(new UserCommandFailedException(
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
                         source.SetException(new UserCommandConflictException(response.StatusDescription));
                     else
                         source.SetException(new UserCommandFailedException(
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
