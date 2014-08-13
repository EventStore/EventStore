using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public interface IOptionSourceProvider
    {
        OptionSource[] GetEffectiveOptions();
    }
}
