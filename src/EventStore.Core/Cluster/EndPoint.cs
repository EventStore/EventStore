namespace EventStore.Cluster {
	public partial class EndPoint {
		public EndPoint(string address, uint port) {
			address_ = address;
			port_ = port;
		}
	}
}
