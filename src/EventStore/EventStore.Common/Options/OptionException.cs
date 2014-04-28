using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace EventStore.Common.Options
{
    [Serializable]
    public class OptionException : Exception
    {
        private string option;

        public OptionException()
        {
        }

        public OptionException(string message, string optionName)
            : base(message)
        {
            this.option = optionName;
        }

        public OptionException(string message, string optionName, Exception innerException)
            : base(message, innerException)
        {
            this.option = optionName;
        }

        protected OptionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.option = info.GetString("OptionName");
        }

        public string OptionName
        {
            get { return this.option; }
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("OptionName", option);
        }
    }
}
