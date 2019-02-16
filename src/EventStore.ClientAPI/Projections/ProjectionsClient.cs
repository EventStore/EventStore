using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using Newtonsoft.Json.Linq;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;
using EventStore.ClientAPI.Common.Utils.Threading;

namespace EventStore.ClientAPI.Projections {
	internal class ProjectionsClient {
		private readonly HttpAsyncClient _client;
		private readonly TimeSpan _operationTimeout;

		public ProjectionsClient(ILogger log, TimeSpan operationTimeout) {
			_operationTimeout = operationTimeout;
			_client = new HttpAsyncClient(_operationTimeout);
		}

		public Task Enable(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/command/enable", name), string.Empty,
				userCredentials, HttpStatusCode.OK);
		}

		public Task Disable(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/command/disable", name), string.Empty,
				userCredentials, HttpStatusCode.OK);
		}

		public Task Abort(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/command/abort", name), string.Empty,
				userCredentials, HttpStatusCode.OK);
		}

		public Task CreateOneTime(EndPoint endPoint, string query, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(endPoint.ToHttpUrl(httpSchema, "/projections/onetime?type=JS"), query, userCredentials,
				HttpStatusCode.Created);
		}

		public Task CreateTransient(EndPoint endPoint, string name, string query,
			UserCredentials userCredentials = null, string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(
				endPoint.ToHttpUrl(httpSchema, "/projections/transient?name={0}&type=JS", name),
				query,
				userCredentials,
				HttpStatusCode.Created);
		}

		public Task CreateContinuous(EndPoint endPoint, string name, string query, bool trackEmitted,
			UserCredentials userCredentials = null, string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(
				endPoint.ToHttpUrl(httpSchema,
					"/projections/continuous?name={0}&type=JS&emit=1&trackemittedstreams={1}", name, trackEmitted),
				query, userCredentials, HttpStatusCode.Created);
		}

		public Task<List<ProjectionDetails>> ListAll(EndPoint endPoint, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projections/any"), userCredentials, HttpStatusCode.OK)
				.ContinueWith(x => {
					if (x.IsFaulted) throw x.Exception;
					var r = JObject.Parse(x.Result);
					return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
				});
		}

		public Task<List<ProjectionDetails>> ListOneTime(EndPoint endPoint, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projections/onetime"), userCredentials, HttpStatusCode.OK)
				.ContinueWith(x => {
					if (x.IsFaulted) throw x.Exception;
					var r = JObject.Parse(x.Result);
					return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
				});
		}

		public Task<List<ProjectionDetails>> ListContinuous(EndPoint endPoint, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projections/continuous"), userCredentials,
					HttpStatusCode.OK)
				.ContinueWith(x => {
					if (x.IsFaulted) throw x.Exception;
					var r = JObject.Parse(x.Result);
					return r["projections"] != null ? r["projections"].ToObject<List<ProjectionDetails>>() : null;
				});
		}

		public Task<string> GetStatus(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}", name), userCredentials, HttpStatusCode.OK);
		}

		public Task<string> GetState(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/state", name), userCredentials,
				HttpStatusCode.OK);
		}

		public Task<string> GetPartitionStateAsync(EndPoint endPoint, string name, string partition,
			UserCredentials userCredentials = null, string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/state?partition={1}", name, partition),
				userCredentials, HttpStatusCode.OK);
		}

		public Task<string> GetResult(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/result", name), userCredentials,
				HttpStatusCode.OK);
		}

		public Task<string> GetPartitionResultAsync(EndPoint endPoint, string name, string partition,
			UserCredentials userCredentials = null, string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/result?partition={1}", name, partition),
				userCredentials, HttpStatusCode.OK);
		}

		public Task<string> GetStatistics(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/statistics", name), userCredentials,
				HttpStatusCode.OK);
		}

		public Task<string> GetQuery(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendGet(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/query", name), userCredentials,
				HttpStatusCode.OK);
		}

		public Task UpdateQuery(EndPoint endPoint, string name, string query, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPut(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/query?type=JS", name), query,
				userCredentials, HttpStatusCode.OK);
		}

		public Task UpdateQuery(EndPoint endPoint, string name, string query, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA, bool? emitEnabled = null) {
			var emit = emitEnabled.HasValue ? (emitEnabled.Value ? "1" : "0") : string.Empty;
			var url = emitEnabled.HasValue ? "/projection/{0}/query?emit={1}" : "/projection/{0}/query";
			return SendPut(endPoint.ToHttpUrl(httpSchema, url, name, emit), query, userCredentials, HttpStatusCode.OK);
		}


		public Task Delete(EndPoint endPoint, string name, bool deleteEmittedStreams,
			UserCredentials userCredentials = null, string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendDelete(
				endPoint.ToHttpUrl(httpSchema, "/projection/{0}?deleteEmittedStreams={1}", name, deleteEmittedStreams),
				userCredentials, HttpStatusCode.OK);
		}

		public Task Reset(EndPoint endPoint, string name, UserCredentials userCredentials = null,
			string httpSchema = EndpointExtensions.HTTP_SCHEMA) {
			return SendPost(endPoint.ToHttpUrl(httpSchema, "/projection/{0}/command/reset", name), string.Empty,
				userCredentials, HttpStatusCode.OK);
		}

		private Task<string> SendGet(string url, UserCredentials userCredentials, int expectedCode) {
			var source = TaskCompletionSourceFactory.Create<string>();
			_client.Get(url,
				userCredentials,
				response => {
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

		private Task<string> SendDelete(string url, UserCredentials userCredentials, int expectedCode) {
			var source = TaskCompletionSourceFactory.Create<string>();
			_client.Delete(url,
				userCredentials,
				response => {
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

		private Task SendPut(string url, string content, UserCredentials userCredentials, int expectedCode) {
			var source = TaskCompletionSourceFactory.Create<object>();
			_client.Put(url,
				content,
				"application/json",
				userCredentials,
				response => {
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

		private Task SendPost(string url, string content, UserCredentials userCredentials, int expectedCode) {
			var source = TaskCompletionSourceFactory.Create<object>();
			_client.Post(url,
				content,
				"application/json",
				userCredentials,
				response => {
					if (response.HttpStatusCode == expectedCode)
						source.SetResult(null);
					else if (response.HttpStatusCode == 409)
						source.SetException(new ProjectionCommandConflictException(response.HttpStatusCode,
							response.StatusDescription));
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
