using System;
using System.Net;

namespace EventStore.Common {
	public class EventStoreEndPoint : EndPoint, IEquatable<EventStoreEndPoint> {
		public string Host { get; }
		public int Port { get; }
		public EventStoreEndPoint(string host, int port) {
			Host = host;
			Port = port;
		}

		public bool Equals(EventStoreEndPoint other)
		{
			if (ReferenceEquals(null, other)) return false;
			if (ReferenceEquals(this, other)) return true;
			return Host == other.Host && Port == other.Port;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((EventStoreEndPoint) obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Host, Port);
		}
		
		public override string ToString() => $"{Host}:{Port}";
	}
}
