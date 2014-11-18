using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// API for managing users in the Event Store through C# code. Communicates
    /// with the Event Store over the RESTful API.
    /// </summary>
    public class UsersManager
    {
        private readonly UsersClient _client;

        private readonly IPEndPoint _httpEndPoint;

        /// <summary>
        /// Creates a new instance of <see cref="UsersManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        /// <param name="operationTimeout"></param>
        public UsersManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan operationTimeout)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _client = new UsersClient(log, operationTimeout);
            _httpEndPoint = httpEndPoint;
        }

        /// <summary>
/// Asynchronously enables a user
/// </summary>
/// <param name="login">The login of the user to enable</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task EnableAsync(string login, UserCredentials userCredentials =null)
        {
Ensure.NotNullOrEmpty(login, "login");
return _client.Enable(_httpEndPoint, login, userCredentials);
        }
        
        /// <summary>
        /// Asynchronously disables a user
        /// </summary>
        /// <param name="login">The login of the user to disable</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisableAsync(string login, UserCredentials userCredentials =null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            return _client.Disable(_httpEndPoint, login, userCredentials);
        }
        /// <summary>
        /// Asynchronously deletes a user
        /// </summary>
        /// <param name="login">The login of the user.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a user</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string login, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(login, "login");
            return _client.Delete(_httpEndPoint, login, userCredentials);
        }
        }
}
