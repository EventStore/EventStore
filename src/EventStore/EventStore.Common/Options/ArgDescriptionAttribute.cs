using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Common.Options
{
    public class ArgDescriptionAttribute : PowerArgs.ArgDescription
    {
        public ArgDescriptionAttribute(string description) : base(description) { }
        public ArgDescriptionAttribute(string description, string group) : base(description, group) { }
    }
}
