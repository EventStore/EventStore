using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Common.CommandLine.lib;

namespace EventStore.Common.CommandLine
{
    public abstract class EventStoreCmdLineOptionsBase : CommandLineOptionsBase
    {
        public abstract IEnumerable<KeyValuePair<string, string>> GetLoadedOptionsPairs();

        [HelpOption]
        public virtual string GetUsage()
        {
            return HelpText.AutoBuild(this, (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }

    }
}
