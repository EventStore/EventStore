using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public static class OptionsSourceMerger
    {
        public static OptionSource[] SequentialMerge(params OptionSource[][] optionsSources)
        {
            var effectiveOptionsSource = new Dictionary<string, OptionSource>();
            foreach (var optionSources in optionsSources.Where(x => x != null))
            {
                foreach (var optionSource in optionSources)
                {
                    if (effectiveOptionsSource.ContainsKey(optionSource.Name))
                    {
                        effectiveOptionsSource[optionSource.Name] = optionSource;
                    }
                    else
                    {
                        effectiveOptionsSource.Add(optionSource.Name, optionSource);
                    }
                }
            }
            return effectiveOptionsSource.Select(x => x.Value).ToArray();
        }
    }
}
