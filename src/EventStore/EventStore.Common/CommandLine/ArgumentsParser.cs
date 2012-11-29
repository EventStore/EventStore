using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using EventStore.Common.Exceptions;

namespace EventStore.Common.CommandLine
{
    public static class ArgumentsParser
    {
        public static IPAddress[] ParseIpsList(string ipsString)
        {
            IPAddress[] addresses;
            if (!string.IsNullOrEmpty(ipsString))
            {
                var list = new List<IPAddress>();
                foreach (var ipStr in ipsString.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    IPAddress ip;
                    if (!IPAddress.TryParse(ipStr, out ip))
                    {
                        throw new ApplicationInitializationException(
                            string.Format("Could not parse IP address [{0}] provided in argument ManagersIPs list {1}.",
                                          ipStr,
                                          ipsString));
                    }
                    list.Add(ip);
                }
                addresses = list.ToArray();
            }
            else
            {
                addresses = new IPAddress[0];
            }
            return addresses;
        }
    }
}
