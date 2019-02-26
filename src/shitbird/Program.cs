using System;
using System.CodeDom;
using System.Net;
using EventStore.ClientAPI;

namespace shitbird {
	internal class Program {
		public static void Main(string[] args) {
			var c1 = EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 40000));
			var c2 = EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 40001));
			//var c3 = EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 40002));
			c1.ConnectAsync().Wait();
			c2.ConnectAsync().Wait();
			
			Console.WriteLine("get sharded connection");
			var conn = new ShardedConnection("shitbird", new []{c1,c2});
			Console.WriteLine("got sharded connection");
			for (var i = 0; i < 1000000; i++) {
				Console.WriteLine("writing " + i);
				conn.AppendToStreamAsync("foo-" + i, ExpectedVersion.Any,
					new EventData(Guid.NewGuid(), "foo fighter", false, new byte[512], new byte[515])).Wait();
			}
		}
	}
}
