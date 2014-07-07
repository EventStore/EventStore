using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PowerArgs
{
    internal class AttrOverride
    {
        Dictionary<string, object> overrideValues;

        public AttrOverride()
        {
            overrideValues = new Dictionary<string, object>();
        }

        internal void Set(object value)
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod() as MethodInfo;

            string propertyName = callingMethod.Name.Substring("get_".Length);

            if (overrideValues.ContainsKey(propertyName))
            {
                overrideValues[propertyName] = value;
            }
            else
            {
                overrideValues.Add(propertyName, value);
            }
        }

        internal T2 Get<T1, T2>(IEnumerable<IArgMetadata> attriibutes, Func<T1, T2> getter, T2 defaultValue = default(T2)) where T1 : Attribute
        {
            StackTrace stackTrace = new StackTrace();
            var callingMethod = stackTrace.GetFrame(1).GetMethod() as MethodInfo;

            string propertyName = callingMethod.Name.Substring("get_".Length);
            string typeName = callingMethod.DeclaringType.Name;

            bool hasOverride = overrideValues.ContainsKey(propertyName);
            bool hasMatchingAttribute = attriibutes.HasMeta<T1>();

            object attributeVal = default(T2);
            object overrideVal = default(T2);

            if (hasOverride)
            {
                overrideVal = overrideValues[propertyName];
            }

            if (hasMatchingAttribute)
            {
                T1 attribute = attriibutes.Meta<T1>();
                attributeVal = getter(attribute);
            }


            if (hasOverride && hasMatchingAttribute && (overrideVal.Equals(attributeVal) == false))
            {
                throw new InvalidArgDefinitionException(string.Format("The property '{0}' has been manually set, and the manual override value of '{2}' conflicts with the value '{3}' retrieved from attribute '{1}'", typeName + "." + propertyName, typeof(T1).Name, overrideVal, attributeVal));
            }
            else if (hasOverride)
            {
                return (T2)overrideVal;
            }
            else if (hasMatchingAttribute)
            {
                return (T2)attributeVal;
            }
            else
            {
                return defaultValue;
            }
        }
    }
}
