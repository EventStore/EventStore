using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace EventStore.Rags.Tests
{
    public class TestType
    {
        public string Name { get; set; }
        public string[] Names { get; set; }
        public bool Flag { get; set; }
        public IPEndPoint IpEndpoint { get; set; }
        public IPEndPoint[] IpEndpoints { get; set; }
        public TestType()
        {
            Flag = false;
            Name = "foo";
            Names = new string[] { "one", "two", "three" };
            IpEndpoint = new IPEndPoint(IPAddress.Loopback, IPEndPoint.MinPort);
            IpEndpoints = new IPEndPoint[]{
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 1113),
                new IPEndPoint(IPAddress.Parse("127.0.0.2"), 1114)
            };
        }
    }
}
