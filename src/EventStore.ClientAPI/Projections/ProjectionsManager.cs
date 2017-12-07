using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;

namespace EventStore.ClientAPI.Projections
{
    /// <summary>
    /// API for managing projections in Event Store through C# code. Communicates
    /// with Event Store over the RESTful API. All methods in this class are asynchronous, and you must have access to a projection to use a method.
    /// </summary>
    public class ProjectionsManager
    {
        private readonly ProjectionsClient _client;
        private readonly EndPoint _httpEndPoint;
        private readonly string _httpSchema;

        /// <summary>
        /// Creates a new instance of <see cref="ProjectionsManager"/>.
        /// </summary>
        /// <param name="log">An instance of <see cref="ILogger"/> to use for logging.</param>
        /// <param name="httpEndPoint">HTTP endpoint of an Event Store server.</param>
        /// <param name="httpSchema">HTTP endpoint schema http|https.</param>
        /// <param name="operationTimeout"></param>
        public ProjectionsManager(ILogger log, EndPoint httpEndPoint, TimeSpan operationTimeout, string httpSchema = EndpointExtensions.HTTP_SCHEMA)
        {
            Ensure.NotNull(log, "log");
            Ensure.NotNull(httpEndPoint, "httpEndPoint");

            _client = new ProjectionsClient(log, operationTimeout);
            _httpEndPoint = httpEndPoint;
            _httpSchema = httpSchema;
        }

        /// <summary>
        /// Enables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to enable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task EnableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Enable(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Disables a projection without writing a checkpoint.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task DisableAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Disable(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Aborts and disables a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to disable a projection.</param>
        /// <returns>A task representing the operation.</returns>
        public Task AbortAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Abort(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Creates a one-time query with JavaScript code. The projection will run until the end of the log and then stop.
        /// </summary>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateOneTimeAsync(string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(query, "query");
            return _client.CreateOneTime(_httpEndPoint, query, userCredentials, _httpSchema);
        }

        /// <summary>
        /// TODO: Missing from docs, needs further explanation.
        /// Creates a one-time query with JavaScript code.
        /// </summary>
        /// <param name="name">A name for the query.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateTransientAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");
            return _client.CreateTransient(_httpEndPoint, name, query, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Creates a continuous projection with JavaScript code that will run until the end of the log and then continue running. Continuous projections have explicit names and you can enable or disable them via this name.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateContinuousAsync(string name, string query, UserCredentials userCredentials = null)
        {
            return CreateContinuousAsync(name, query, false, userCredentials);
        }

        /// <summary>
        /// TODO: Missing from docs, needs further explanation, mostly the additional parameter.
        /// Creates a continuous projection with JavaScript code that will run until the end of the log and then continue running. Continuous projections have explicit names and you can enable or disable them via this name.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="query">The JavaScript source code for the query.</param>
        /// <param name="trackEmittedStreams">Whether the streams emitted by this projection should be tracked.</param>
        /// <param name="userCredentials">Credentials for a user with permission to create a query.</param>
        public Task CreateContinuousAsync(string name, string query, bool trackEmittedStreams, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.CreateContinuous(_httpEndPoint, name, query, trackEmittedStreams, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        [Obsolete("Use 'Task<List<ProjectionDetails>> ListAll' instead")]
        public Task<string> ListAllAsStringAsync(UserCredentials userCredentials = null)
        {
            return _client.ListAllAsString(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// TODO: Can below be a proper object link?
        /// <returns>List of all ProjectionDetails items containing projection statuses.</returns>
        public Task<List<ProjectionDetails>> ListAllAsync(UserCredentials userCredentials = null)
        {
            return _client.ListAll(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all one-time projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        [Obsolete("Use 'Task<List<ProjectionDetails>> ListOneTime' instead")]
        public Task<string> ListOneTimeAsStringAsync(UserCredentials userCredentials = null)
        {
            return _client.ListOneTimeAsString(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all one-time projections.
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>List of one-time ProjectionDetails items containing projection statuses.</returns>
        public Task<List<ProjectionDetails>> ListOneTimeAsync(UserCredentials userCredentials = null)
        {
            return _client.ListOneTime(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all continuous projections.
        /// TODO: Difference?
        /// TODO: Wasn't in docs
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statuses.</returns>
        [Obsolete("Use 'Task<List<ProjectionDetails>> ListContinuous' instead")]
        public Task<string> ListContinuousAsStringAsync(UserCredentials userCredentials = null)
        {
            return _client.ListContinuousAsString(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Lists the status of all continuous projections.
        /// TODO: Wasn't in docs
        /// </summary>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>List of continuous ProjectionDetails items containing projection statuses.</returns>
        public Task<List<ProjectionDetails>> ListContinuousAsync(UserCredentials userCredentials = null)
        {
            return _client.ListContinuous(_httpEndPoint, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the status of a projection.
        /// TODO: Wasn't in docs
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection status.</returns>
        public Task<string> GetStatusAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatus(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the state of a projection.
        /// TODO: Wasn't in docs
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public Task<string> GetStateAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetState(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the state of a projection for a specified partition.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="partitionId">The id of the partition.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public Task<string> GetPartitionStateAsync(string name, string partitionId, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(partitionId, "partitionId");
            return _client.GetPartitionStateAsync(_httpEndPoint, name, partitionId, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the state of a projection.
        /// Wasn't in docs
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public Task<string> GetResultAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetResult(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the result of a projection for a specified partition.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="partitionId">The id of the partition.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection state.</returns>
        public Task<string> GetPartitionResultAsync(string name, string partitionId, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(partitionId, "partitionId");
            return _client.GetPartitionResultAsync(_httpEndPoint, name, partitionId, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Returns the statistics associated with a named projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing projection statistics.</returns>
        public Task<string> GetStatisticsAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetStatistics(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Gets the status of a query.
        /// TODO: Wasn't in docs.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        /// <returns>String of JSON containing query status.</returns>
        public Task<string> GetQueryAsync(string name, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.GetQuery(_httpEndPoint, name, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Updates the definition of a query.
        /// TODO: Wasn't in docs.
        /// </summary>
        /// <param name="name">The name of the query.</param>
        /// <param name="query">The JavaScript source code of the query.</param>
        /// <param name="userCredentials">Credentials for the operation.</param>
        public Task UpdateQueryAsync(string name, string query, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            Ensure.NotNullOrEmpty(query, "query");

            return _client.UpdateQuery(_httpEndPoint, name, query, userCredentials, _httpSchema);
        }

        /// <summary>
        /// Deletes a projection.
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string name, UserCredentials userCredentials = null)
        {
            return DeleteAsync(name, false, userCredentials);
        }

        /// <summary>
        /// Deletes a projection.
        /// TODO: Difference?
        /// </summary>
        /// <param name="name">The name of the projection.</param>
        /// <param name="deleteEmittedStreams">Whether to delete the streams that were emitted by this projection.</param>
        /// <param name="userCredentials">Credentials for a user with permission to delete a projection</param>
        /// <returns>A task representing the operation.</returns>
        public Task DeleteAsync(string name, bool deleteEmittedStreams, UserCredentials userCredentials = null)
        {
            Ensure.NotNullOrEmpty(name, "name");
            return _client.Delete(_httpEndPoint, name, deleteEmittedStreams, userCredentials, _httpSchema);
        }
    }
}
