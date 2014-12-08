using System;
using System.Net;
using System.Threading.Tasks;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI.UserManagement
{
    internal class UsersClient
    {
        private readonly HttpAsyncClient _client;

        private readonly TimeSpan _operationTimeout;

        public UsersClient(ILogger log, TimeSpan operationTimeout)
        {
            this._operationTimeout = operationTimeout;
            this._client = new HttpAsyncClient(log);
        }

public Task Enable(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return this.SendPost(endPoint.ToHttpUrl("/users/{login}/command/enable", login), string.Empty, userCredentials, HttpStatusCode.OK);
    }

public  Task Disable(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return this.SendPost(endPoint.ToHttpUrl("/users/{login}/command/disable", login), string.Empty, userCredentials, HttpStatusCode.OK);
        }

        public Task Delete(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
{
    return this.SendDelete(endPoint.ToHttpUrl("/users/{login}", login), userCredentials, HttpStatusCode.OK);
}
        
        public Task<string> ListAll(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return this.SendGet(endPoint.ToHttpUrl("/users/"), userCredentials, HttpStatusCode.OK);
        }
        
        public Task<string> GetCurrentUser(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return this.SendGet(endPoint.ToHttpUrl("/users/$current"), userCredentials, HttpStatusCode.OK);
        }

        public Task<string> GetUser(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return this.SendGet(endPoint.ToHttpUrl("/users/{login}", login), userCredentials, HttpStatusCode.OK);
        }
        
        public Task CreateUser(IPEndPoint endPoint, UserCreationInformation newUser, UserCredentials userCredentials = null)
        {
            string userJson = Json.ToJson(newUser);
            return this.SendPost(endPoint.ToHttpUrl("/users/"), userJson, userCredentials, HttpStatusCode.Created);
        }
        
        public Task UpdateUser(IPEndPoint endPoint, string login, UserUpdateInformation updatedUser, UserCredentials userCredentials)
        {
            string userJson = Json.ToJson(updatedUser);
return             this.SendPut(endPoint.ToHttpUrl("/users/{login}", login), userJson, userCredentials, HttpStatusCode.OK);
        }

        public Task ChangePassword(IPEndPoint endPoint, string login, ChangePasswordDetails changePasswordDetails, UserCredentials userCredentials)
{
    string changePasswordJson = Json.ToJson(changePasswordDetails);
    return this.SendPost(endPoint.ToHttpUrl("/users/{login}/command/change-password", login), changePasswordJson, userCredentials, HttpStatusCode.OK);
}

        public Task ResetPassword(IPEndPoint endPoint, string login, ResetPasswordDetails resetPasswordDetails, UserCredentials userCredentials = null)
        {
            string resetPasswordJson = Json.ToJson(resetPasswordDetails);
            return this.SendPost(endPoint.ToHttpUrl("/users/{login}/command/reset-password", login), resetPasswordJson, userCredentials, HttpStatusCode.OK);
        }
        private Task<string> SendGet(string url, UserCredentials userCredentials, int expectedCode)
{
    var source = new TaskCompletionSource<string>();
    this._client.Get(url,
                userCredentials,
                this._operationTimeout,
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
    this._client.Delete(url,
                   userCredentials,
                   this._operationTimeout,
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
    this._client.Put(url,
                content,
                "application/json",
                userCredentials,
                this._operationTimeout,
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
    this._client.Post(url,
                 content,
                 "application/json",
                 this._operationTimeout,
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
