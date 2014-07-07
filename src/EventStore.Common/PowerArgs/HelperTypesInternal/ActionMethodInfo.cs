using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PowerArgs
{
    internal class ActionMethodInfo : MethodInfo
    {
        protected Action<CommandLineArgumentsDefinition> action;

        public ActionMethodInfo(Action<CommandLineArgumentsDefinition> action)
        {
            this.action = action;
        }

        public override ParameterInfo[] GetParameters()
        {
            return new ParameterInfo[]
            {
                new DefinitionParameter()
            };
        }

        public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, System.Globalization.CultureInfo culture)
        {
            action((CommandLineArgumentsDefinition)parameters[0]);
            return null;
        }
        #region NotImplemented

        public override MethodInfo GetBaseDefinition()
        {
            throw new NotImplementedException();
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes
        {
            get { throw new NotImplementedException(); }
        }

        public override MethodAttributes Attributes
        {
            get { return MethodAttributes.Static; }
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            throw new NotImplementedException();
        }

        public override RuntimeMethodHandle MethodHandle
        {
            get { throw new NotImplementedException(); }
        }

        public override Type DeclaringType
        {
            get { throw new NotImplementedException(); }
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            throw new NotImplementedException();
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            throw new NotImplementedException();
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        public override Type ReflectedType
        {
            get { throw new NotImplementedException(); }
        } 
        #endregion
    }

    internal class DefinitionParameter : ParameterInfo
    {
        public override Type ParameterType
        {
            get
            {
                return typeof(CommandLineArgumentsDefinition);
            }
        }
    }
}
