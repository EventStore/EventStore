using System;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// API for managing projections in the Event Store through C# code. Communicates
    /// with the Event Store over the RESTful API.
    /// </summary>
    public class ProjectionsManager
    {
        private readonly ProjectionsClient _client;
        private readonly IPEndPoint _httpEndPoint;

        /// <summary>
        /// Creates a new instance of <see cref="ProjectionsManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        /// <param name="operationTimeout"></param>
        public ProjectionsManager(ILogger log, IPEndPoint httpEndPoint, TimeSpan operationTimeout)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _client = new ProjectionsClient(log, operationTimeout);
            _httpEndPoint = httpEndPoint;
        }

        /// <summary>
        /// Synchronously enables a projection.
        /// </summary>
        /// <param name="name">The name of the projection</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a projection</param>
        public void Enable(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            EnableAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously enables a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task EnableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Enable(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously disables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        public void Disable(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DisableAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously aborts and disables a projection without writing a checkpoint.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Disable(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously avborts and disables a projection without writing a checkpoint.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        public void Abort(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            AbortAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously disables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task AbortAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Abort(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously creates a one-time query.
        /// </summary>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public void CreateOneTime(string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(query, "query");
            CreateOneTimeAsync(query, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously creates a one-time query.
        /// </summary>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateOneTimeAsync(string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(query, "query");
            return _client.CreateOneTime(_httpEndPoint, query, userCredentials);
        }

        /// <summary>
        /// Synchronously creates a continuous projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public void CreateContinuous(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            CreateContinuousAsync(name, query, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously creates a continuous projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateContinuousAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.CreateContinuous(_httpEndPoint, name, query, userCredentials);
        }

        /// <summary>
        /// Synchronously lists this status of all projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public string ListAll(UserCredentials userCredentials = null)
        {
            return ListAllAsync(userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously lists this status of all projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public Task<string> ListAllAsync(UserCredentials userCredentials = null)
        {
            return _client.ListAll(_httpEndPoint, userCredentials);
        }

        /// <summary>
        /// Synchronously lists this status of all one-time projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public string ListOneTime(UserCredentials userCredentials = null)
        {
            return ListOneTimeAsync(userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously lists this status of all one-time projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public Task<string> ListOneTimeAsync(UserCredentials userCredentials = null)
        {
            return _client.ListOneTime(_httpEndPoint, userCredentials);
        }

        /// <summary>
        /// Synchronously lists this status of all continuous projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public string ListContinuous(UserCredentials userCredentials = null)
        {
            return ListContinuousAsync(userCredentials).Result;
        }

        /// <summary>
        /// Synchronously lists this status of all continuous projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        public Task<string> ListContinuousAsync(UserCredentials userCredentials = null)
        {
            return _client.ListContinuous(_httpEndPoint, userCredentials);
        }

        /// <summary>
        /// Synchronously gets the status of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection status.</returns>
        public string GetStatus(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatusAsync(name, userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously gets the status of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection status.</returns>
        public Task<string> GetStatusAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatus(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously gets the state of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public string GetState(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStateAsync(name, userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously gets the state of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public Task<string> GetStateAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetState(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously gets the statistics of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statistics.</returns>
        public string GetStatistics(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetStatisticsAsync(name, userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously gets the statistics of a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statistics.</returns>
        public Task<string> GetStatisticsAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatistics(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously gets the status of a query.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing query status.</returns>
        public string GetQuery(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return GetQueryAsync(name, userCredentials).Result;
        }

        /// <summary>
        /// Asynchronously gets the status of a query.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing query status.</returns>
        public Task<string> GetQueryAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetQuery(_httpEndPoint, name, userCredentials);
        }

        /// <summary>
        /// Synchronously updates the definition of a query.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="query">The JavaScript source code of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        public void UpdateQuery(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            UpdateQueryAsync(name, query, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously updates the definition of a query.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="query">The JavaScript source code of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        public Task UpdateQueryAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.UpdateQuery(_httpEndPoint, name, query, userCredentials);
        }

        /// <summary>
        /// Synchronously deletes a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        public void Delete(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            DeleteAsync(name, userCredentials).Wait();
        }

        /// <summary>
        /// Asynchronously deletes a projection 
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Delete(_httpEndPoint, name, userCredentials);
        }
    }
}
