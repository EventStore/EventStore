using System.Net.Http;

namespace EventStore.Core.Services.Transport.Http.NodeHttpClientFactory;

public interface INodeHttpClientFactory {
	HttpClient CreateHttpClient(string[] additionalCertificateNames);
}
