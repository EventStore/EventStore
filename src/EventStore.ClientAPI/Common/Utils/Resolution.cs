using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace EventStore.ClientAPI.Common.Utils {
    static class Resolution {
	    private static readonly Random random = new Random();
	    private static void Shuffle<T>(IList<T> list) {
		    for(int i=list.Count-1;i>0;i--){
			    int k = random.Next(i+1);
			    T value = list[k];
			    list[k] = list[i];
			    list[i] = value;
		    }
	    }

        public static IPAddress Resolve(string name) {
            IPAddress addr;
            if (!IPAddress.TryParse(name, out addr)) {
                var dnsEntry = Dns.GetHostEntry(name);

                if (dnsEntry.AddressList == null || dnsEntry.AddressList.Length == 0)
                    throw new Exception(string.Format("Can't resolve external Tcp host '{0}'", name));

                var addressList = dnsEntry.AddressList.ToList();
                Shuffle(addressList);
                return addressList[0];
            }

            return addr;
        }
    }
}
