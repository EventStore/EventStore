using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.Tests.Integration;
using EventStore.Projections.Core.Tests.ClientAPI.Cluster;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.Transport.Http {
	public class Authorization : specification_with_standard_projections_runnning {
		private Dictionary<string, HttpClient> _httpClients = new Dictionary<string, HttpClient>();
		private TimeSpan _timeout = TimeSpan.FromSeconds(10);
		private int _masterId;
		
		private HttpClient CreateHttpClient(string username, string password){
			var httpClientHandler = new HttpClientHandler();
			httpClientHandler.AllowAutoRedirect = false;

			var client = new HttpClient(httpClientHandler);
			client.Timeout = _timeout;
			client.DefaultRequestHeaders.Authorization = 
				new AuthenticationHeaderValue(
					"Basic", System.Convert.ToBase64String(
						System.Text.ASCIIEncoding.ASCII.GetBytes(
						$"{username}:{password}")));

			return client;
		}

		private int SendRequest(HttpClient client, HttpMethod method, string url, string body, string contentType){
			var request = new HttpRequestMessage();
			request.Method = method;
			request.RequestUri = new Uri(url);

			if(body != null){
				var bodyBytes = Helper.UTF8NoBom.GetBytes(body);
				var stream = new MemoryStream(bodyBytes);
				var content = new StreamContent(stream);
				content.Headers.ContentLength = bodyBytes.Length;
				if(contentType != null)
					content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
				request.Content = content;
			}

			var result = client.SendAsync(request).Result;
			return (int) result.StatusCode;
		}

        private HttpMethod GetHttpMethod(string method)
        {
            switch(method){
				case "GET":
					return HttpMethod.Get;
				case "POST":
					return HttpMethod.Post;
				case "PUT":
					return HttpMethod.Put;
				case "DELETE":
					return HttpMethod.Delete;
				default:
					throw new Exception("Unknown Http Method");
			}
        }

        private int GetAuthLevel(string userAuthorizationLevel)
        {
            switch(userAuthorizationLevel){
				case "None":
					return 0;
				case "User":
					return 1;
				case "Ops":
					return 2;
				case "Admin":
					return 3;
				default:
					throw new Exception("Unknown authorization level");
			}
        }
		public void CreateUser(string username, string password){
			for(int trial=1;trial<=5;trial++){
				try{
					var dataStr = string.Format("{{loginName: '{0}', fullName: '{1}', password: '{2}', groups: []}}", username, username, password);
					var data = Helper.UTF8NoBom.GetBytes(dataStr);
					var stream = new MemoryStream(data);
					var content = new StreamContent(stream);
					content.Headers.Add("Content-Type", "application/json");

					var res = _httpClients["Admin"].PostAsync(
						string.Format("http://{0}/users/", _nodes[_masterId].ExternalHttpEndPoint),
						content
					).Result;
					res.EnsureSuccessStatusCode();
					break;
				}
				catch(HttpRequestException){
					if(trial == 5){
						throw new Exception(string.Format("Error creating user: {0}", username));
					}
					Task.Delay(1000).Wait();
				}
			}
		}

		protected override void Given() {
			base.Given();
			//find the master node
			for(int i=0;i<_nodes.Length;i++){
				if(_nodes[i].NodeState == EventStore.Core.Data.VNodeState.Master){
					_masterId = i;
					break;
				}
			}

			_httpClients["Admin"] = CreateHttpClient("admin", "changeit");
			_httpClients["Ops"] = CreateHttpClient("ops", "changeit");
			CreateUser("user","changeit");
			_httpClients["User"] = CreateHttpClient("user", "changeit");
			_httpClients["None"] = new HttpClient();
		}

        [OneTimeTearDown]
		public override void TestFixtureTearDown() {
			foreach(var kvp in _httpClients){
				kvp.Value.Dispose();
			}
			base.TestFixtureTearDown();
		}

		[Test, Combinatorial]
		public void authorization_tests(
			[Values(
				"None",
				"User",
				"Ops",
				"Admin"
			)] string userAuthorizationLevel,
			[Values(
				false,
				true
			)] bool useInternalEndpoint,
			[Values(
				"/web/es/js/projections/{*remaining_path};GET;None",
				"/web/es/js/projections/v8/Prelude/{*remaining_path};GET;None",
				"/web/projections;GET;None",
				"/projections;GET;User",
				"/projections/any;GET;User",
				"/projections/all-non-transient;GET;User",
				"/projections/transient;GET;User",
				"/projections/onetime;GET;User",
				"/projections/continuous;GET;User",
				"/projections/transient?name=name&type=type&enabled={enabled};POST;User", /* /projections/transient?name={name}&type={type}&enabled={enabled} */
				"/projections/onetime?name=name&type=type&enabled={enabled}&checkpoints={checkpoints}&emit={emit}&trackemittedstreams={trackemittedstreams};POST;Ops", /* /projections/onetime?name={name}&type={type}&enabled={enabled}&checkpoints={checkpoints}&emit={emit}&trackemittedstreams={trackemittedstreams} */
				"/projections/continuous?name=name&type=type&enabled={enabled}&emit={emit}&trackemittedstreams={trackemittedstreams};POST;Ops", /* /projections/continuous?name={name}&type={type}&enabled={enabled}&emit={emit}&trackemittedstreams={trackemittedstreams} */
				"/projection/name/query?config={config};GET;User", /* /projection/{name}/query?config={config} */
				"/projection/name/query?type={type}&emit={emit};PUT;User", /* /projection/{name}/query?type={type}&emit={emit} */
				"/projection/name;GET;User", /* /projection/{name} */
				"/projection/name?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}&deleteEmittedStreams={deleteEmittedStreams};DELETE;Ops", /* /projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}&deleteEmittedStreams={deleteEmittedStreams} */
				"/projection/name/statistics;GET;User", /* projection/{name}/statistics */
				"/projections/read-events;POST;User",
				"/projection/{name}/state?partition={partition};GET;User",
				"/projection/{name}/result?partition={partition};GET;User",
				"/projection/{name}/command/disable?enableRunAs={enableRunAs};POST;User",
				"/projection/{name}/command/enable?enableRunAs={enableRunAs};POST;User",
				"/projection/{name}/command/reset?enableRunAs={enableRunAs};POST;User",
				"/projection/{name}/command/abort?enableRunAs={enableRunAs};POST;User",
				"/projection/{name}/config;GET;Ops",
				"/projection/{name}/config;PUT;Ops"
				/*"/sys/subsystems;GET;Ops"*/ /* this endpoint has been commented since this controller is not registered when using a MiniNode */
			)] string httpEndpointDetails
		){
			/*use the master node endpoint to avoid any redirects*/
			var nodeEndpoint = useInternalEndpoint? _nodes[_masterId].InternalHttpEndPoint: _nodes[_masterId].ExternalHttpEndPoint;
			var httpEndpointTokens = httpEndpointDetails.Split(';');
			var endpointUrl = httpEndpointTokens[0];
			var httpMethod = GetHttpMethod(httpEndpointTokens[1]);
			var requiredMinAuthorizationLevel = httpEndpointTokens[2];

			var url = string.Format("http://{0}{1}", nodeEndpoint, endpointUrl);
			var body = GetData(httpMethod, endpointUrl);
			var contentType = httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Delete ? "application/json" : null;
			var statusCode = SendRequest(_httpClients[userAuthorizationLevel], httpMethod, url, body, contentType);

			if(GetAuthLevel(userAuthorizationLevel) >= GetAuthLevel(requiredMinAuthorizationLevel)){
				Assert.AreNotEqual(401, statusCode);
			} else{
				Assert.AreEqual(401, statusCode);
			}
		}

        private string GetData(HttpMethod httpMethod, string url)
        {
            if(httpMethod == HttpMethod.Post || httpMethod == HttpMethod.Put || httpMethod == HttpMethod.Delete){
				return "{}";
			} else {
				return null;
			}
        }
    }
}
