namespace EventStore.Core.Services.Transport.Http.Messages {
	[StatsGroup("http")] //qq duplicate name
	public enum MessageType {
		None = 0,
		AuthenticatedHttpRequestMessage = 1,
		IncomingHttpRequestMessage = 2,
	}
}
