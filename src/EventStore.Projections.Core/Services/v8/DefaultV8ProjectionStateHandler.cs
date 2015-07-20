using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Util;

namespace EventStore.Projections.Core.Services.v8
{
    public class DefaultV8ProjectionStateHandler : V8ProjectionStateHandler
    {
        private static readonly string _jsPath = Path.Combine(Locations.DefaultContentDirectory, "Prelude");

        public DefaultV8ProjectionStateHandler(
            string query, Action<string, object[]> logger, Action<int, Action> cancelCallbackFactory)
            : base("1Prelude", query, GetModuleSource, logger, cancelCallbackFactory)
        {
        }

        public static Tuple<string, string> GetModuleSource(string name)
        {
            var fullScriptFileName = Path.GetFullPath(Path.Combine(_jsPath, name + ".js"));
            var scriptSource = File.ReadAllText(fullScriptFileName, Helper.UTF8NoBom);
            return Tuple.Create(scriptSource, fullScriptFileName);
        }
    }
}
