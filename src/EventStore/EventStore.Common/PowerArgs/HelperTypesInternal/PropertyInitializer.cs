using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    internal static class PropertyInitializer
    {
        internal static T CreateInstance<T>(int maxDepth = 1)
        {
            return (T)CreateInstance(typeof(T), maxDepth);
        }

        internal static object CreateInstance(Type t, int maxDepth = 1)
        {
            var ret = Activator.CreateInstance(t,true);
            InitializeFields(ret, maxDepth);
            return ret;
        }

        internal static void InitializeFields(this object o, int maxDepth)
        {
            if (maxDepth == 0) return;

            foreach (var prop in o.GetType().GetProperties())
            {
                if (ShouldInitialize(prop))
                {
                    prop.SetValue(o, CreateInstance(prop.PropertyType, maxDepth - 1), null);
                }
            }
        }

        private static bool ShouldInitialize(PropertyInfo prop)
        {
            if (prop.PropertyType.GetInterfaces().Contains(typeof(IList)) == false) return false;
            if(prop.GetSetMethod(true) == null)return false;
            if (prop.PropertyType.IsGenericType == false) return false;

            return true;
        }
    }
}
