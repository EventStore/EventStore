using System;
using System.Reflection;

namespace PowerArgs
{
    internal class ArgActionMethodVirtualProperty : VirtualPropertyInfo
    {
        MethodInfo method;
        public object Value { get; set; }

        public ArgActionMethodVirtualProperty(MethodInfo method)
        {
            this.method = method;
        }

        public override string Name
        {
            get { return method.Name; }
        }
    }
}
