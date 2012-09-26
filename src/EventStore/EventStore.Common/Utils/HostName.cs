using System;
using System.Diagnostics;

namespace EventStore.Common.Utils
{
    public class HostName
    {
        public static string Combine(string hostName, string relativeUri, params object[] arg)
        {
            try
            {
                return CombineHostNameAndPath(hostName, relativeUri, arg);
            }
            catch (Exception e)
            {
                Debug.WriteLine("Failed to combine hostname with relative path: {0}", e.Message);
                return hostName;
            }
        }

        private static string CombineHostNameAndPath(string hostName,
                                                     string relativeUri,
                                                     object[] arg)
        {
            var path = string.Format(relativeUri, arg);

            if (hostName.Contains(":"))
            {
                var parts = hostName.Split(new[] {":"}, StringSplitOptions.RemoveEmptyEntries);
                return new Uri(new UriBuilder(Uri.UriSchemeHttp, parts[0], Int32.Parse(parts[1])).Uri, path).ToString();
            }

            return new Uri(new UriBuilder(Uri.UriSchemeHttp, hostName).Uri, path).ToString();
        }
    }
}
