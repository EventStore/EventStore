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
        public bool Flag { get; set; }
        public IPEndPoint IpEndpoint { get; set; }
        public TestType()
        {
            Flag = false;
            Name = "foo";
            IpEndpoint = new IPEndPoint(IPAddress.Loopback, IPEndPoint.MinPort);
        }
    }
}
