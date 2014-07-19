using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using EventStore.Common.Utils;

namespace EventStore.Projections.Core.Services.v8
{
    public class DefaultV8ProjectionStateHandler : V8ProjectionStateHandler
    {
        private static readonly string _jsPath = Path.Combine(GetJsFileSystemDirectory(), "Prelude");

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

        public static string GetJsFileSystemDirectory()
        {
            string fileSystemWebRoot;
            try
            {
                var sf = new StackFrame(0, true);
                var fileName = sf.GetFileName();
                var sourceWebRootDirectory = string.IsNullOrEmpty(fileName)
                    ? ""
                    : Path.GetFullPath(Path.Combine(fileName, @"..\..\.."));
                fileSystemWebRoot = Directory.Exists(sourceWebRootDirectory)
                    ? sourceWebRootDirectory
                    : AppDomain.CurrentDomain.BaseDirectory;
            }
            catch (Exception)
            {
                fileSystemWebRoot = AppDomain.CurrentDomain.BaseDirectory;
            }
            return fileSystemWebRoot;
        }

    }
}
