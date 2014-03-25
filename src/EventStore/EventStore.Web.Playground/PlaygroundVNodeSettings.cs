using System.Net;
using EventStore.Common.Utils;

namespace EventStore.Web.Playground
{
    public class PlaygroundVNodeSettings
    {
        public readonly IPEndPoint ExternalTcpEndPoint;
        public readonly IPEndPoint ExternalHttpEndPoint;
        public readonly string[] HttpPrefixes;
        public readonly int WorkerThreads;


        public PlaygroundVNodeSettings(IPEndPoint externalTcpEndPoint, IPEndPoint externalHttpEndPoint, string[] httpPrefixes,
            int workerThreads)
        {
            Ensure.NotNull(externalTcpEndPoint, "externalTcpEndPoint");
            Ensure.NotNull(externalHttpEndPoint, "externalHttpEndPoint");
            Ensure.NotNull(httpPrefixes, "httpPrefixes");
            Ensure.Positive(workerThreads, "workerThreads");

            ExternalTcpEndPoint = externalTcpEndPoint;
            ExternalHttpEndPoint = externalHttpEndPoint;
            HttpPrefixes = httpPrefixes;
            WorkerThreads = workerThreads;
        }

        public override string ToString()
        {
            return string.Format("ExternalTcpEndPoint: {0},\n"
                                 + "ExternalHttpEndPoint: {1},\n"
                                 + "HttpPrefixes: {2},\n"
                                 + "WorkerThreads: {3}\n",
                                 ExternalTcpEndPoint,
                                 ExternalHttpEndPoint,
                                 string.Join(", ", HttpPrefixes),
                                 WorkerThreads);
        }
    }
}
