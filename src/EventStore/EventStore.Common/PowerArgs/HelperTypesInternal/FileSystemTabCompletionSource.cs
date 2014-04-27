using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace PowerArgs
{
    internal class FileSystemTabCompletionSource : ITabCompletionSource
    {
        string lastSoFar = null, lastCompletion = null;
        int tabIndex = -1;
        public bool TryComplete(bool shift, string soFar, out string completion)
        {
            completion = null;
            try
            {
                soFar = soFar.Replace("\"", "");
                if (soFar == "")
                {
                    soFar = lastSoFar ?? ".\\";
                }

                if (soFar == lastCompletion)
                {
                    soFar = lastSoFar;
                }
                else
                {
                    tabIndex = -1;
                }

                var dir = Path.GetDirectoryName(soFar);

                if (Path.IsPathRooted(soFar) == false)
                {
                    dir = Environment.CurrentDirectory;
                    soFar = ".\\" + soFar;
                }

                if (Directory.Exists(dir) == false)
                {
                    return false;
                }
                var rest = Path.GetFileName(soFar);

                var matches = from f in Directory.GetFiles(dir)
                              where f.ToLower().StartsWith(Path.GetFullPath(soFar).ToLower())
                              select f;

                var matchesArray = (matches.Union(from d in Directory.GetDirectories(dir)
                                                  where d.ToLower().StartsWith(Path.GetFullPath(soFar).ToLower())
                                                  select d)).ToArray();

                if (matchesArray.Length > 0)
                {
                    tabIndex = shift ? tabIndex - 1 : tabIndex + 1;
                    if (tabIndex < 0) tabIndex = matchesArray.Length - 1;
                    if (tabIndex >= matchesArray.Length) tabIndex = 0;

                    completion = matchesArray[tabIndex];

                    if (completion.Contains(" "))
                    {
                        completion = '"' + completion + '"';
                    }
                    lastSoFar = soFar;
                    lastCompletion = completion.Replace("\"", "");
                    return true;
                }
                else
                {
                    lastSoFar = null;
                    lastCompletion = null;
                    tabIndex = -1;
                    return false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception ex)
            {
                // TODO P2 - Why do we have tracing here?
                Trace.TraceError(ex.ToString());
                return false;  // We don't want a bug in this logic to break the app
            }
        }
    }
}
