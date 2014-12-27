using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
            _operationTimeout = operationTimeout;
            _client = new HttpAsyncClient(log);
        }

        public Task Enable(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/users/{0}/command/enable", login), string.Empty,
                userCredentials, HttpStatusCode.OK);
        }

        public Task Disable(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return SendPost(endPoint.ToHttpUrl("/users/{0}/command/disable", login), string.Empty,
                userCredentials, HttpStatusCode.OK);
        }

        public Task Delete(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return SendDelete(endPoint.ToHttpUrl("/users/{0}", login), userCredentials, HttpStatusCode.OK);
        }

        public Task<List<UserDetails>> ListAll(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/users/"), userCredentials, HttpStatusCode.OK)
                    .ContinueWith(x =>
                    {
                        if (x.IsFaulted) throw x.Exception;
                        var r = JObject.Parse(x.Result);
                        return r["data"] != null ? r["data"].ToObject<List<UserDetails>>() : null;
                    });
        }

        public Task<UserDetails> GetCurrentUser(IPEndPoint endPoint, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/users/$current"), userCredentials, HttpStatusCode.OK)
                .ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    var r = JObject.Parse(x.Result);
                    return r["data"] != null ? r["data"].ToObject<UserDetails>() : null;
                });
        }

        public Task<UserDetails> GetUser(IPEndPoint endPoint, string login, UserCredentials userCredentials = null)
        {
            return SendGet(endPoint.ToHttpUrl("/users/{0}", login), userCredentials, HttpStatusCode.OK)
                .ContinueWith(x =>
                {
                    if (x.IsFaulted) throw x.Exception;
                    var r = JObject.Parse(x.Result);
                    return r["data"] != null ? r["data"].ToObject<UserDetails>() : null;
                });
        }

        public Task CreateUser(IPEndPoint endPoint, UserCreationInformation newUser,
            UserCredentials userCredentials = null)
        {
            var userJson = newUser.ToJson();
            return SendPost(endPoint.ToHttpUrl("/users/"), userJson, userCredentials, HttpStatusCode.Created);
        }

        public Task UpdateUser(IPEndPoint endPoint, string login, UserUpdateInformation updatedUser,
            UserCredentials userCredentials)
        {
            var userJson = updatedUser.ToJson();
            return SendPut(endPoint.ToHttpUrl("/users/{0}", login), userJson, userCredentials,
                HttpStatusCode.OK);
        }

        public Task ChangePassword(IPEndPoint endPoint, string login, ChangePasswordDetails changePasswordDetails,
            UserCredentials userCredentials)
        {
            var changePasswordJson = changePasswordDetails.ToJson();
            return SendPost(endPoint.ToHttpUrl("/users/{0}/command/change-password", login), changePasswordJson,
                userCredentials, HttpStatusCode.OK);
        }

        public Task ResetPassword(IPEndPoint endPoint, string login, ResetPasswordDetails resetPasswordDetails,
            UserCredentials userCredentials = null)
        {
            var resetPasswordJson = resetPasswordDetails.ToJson();
            return SendPost(endPoint.ToHttpUrl("/users/{0}/command/reset-password", login), resetPasswordJson,
                userCredentials, HttpStatusCode.OK);
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
                _operationTimeout,
                response =>
                {
                    if (response.HttpStatusCode == expectedCode)
                        source.SetResult(response.Body);
                    else
                        source.SetException(new UserCommandFailedException(
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
                _operationTimeout,
                response =>
                {
                    if (response.HttpStatusCode == expectedCode)
                        source.SetResult(null);
                    else
                        source.SetException(new UserCommandFailedException(
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
                _operationTimeout,
                userCredentials,
                response =>
                {
                    if (response.HttpStatusCode == expectedCode)
                        source.SetResult(null);
                    else if (response.HttpStatusCode == 409)
                        source.SetException(new UserCommandConflictException(response.HttpStatusCode, response.StatusDescription));
                    else
                        source.SetException(new UserCommandFailedException(
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
