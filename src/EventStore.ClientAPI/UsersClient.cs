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

        internal Task Enable(IPEndPoint _httpEndPoint, string login, UserCredentials userCredentials)
        {
            throw new NotImplementedException();
        }

        internal Task Disable(IPEndPoint _httpEndPoint, string login, UserCredentials userCredentials)
        {
            throw new NotImplementedException();
        }
    }
}
