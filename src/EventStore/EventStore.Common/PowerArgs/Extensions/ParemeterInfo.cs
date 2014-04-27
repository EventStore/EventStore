using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    /// <summary>
    /// Some useful helper extensions for the ParameterInfo type
    /// </summary>
    public static class ParemeterInfoEx
    {
        static Dictionary<string, object> cachedAttributes = new Dictionary<string, object>();

        /// <summary>
        /// Returns true if the given parameter has an attribute of the given type (including inherited types).
        /// </summary>
        /// <typeparam name="T">The type of attribute to test for (will return true for attributes that inherit from this type)</typeparam>
        /// <param name="info">The parameter to test</param>
        /// <returns>true if a matching attribute was found, false otherwise</returns>
        public static bool HasAttr<T>(this ParameterInfo info)
        {
            return info.Attrs<T>().Count > 0;
        }

        /// <summary>
        /// Gets the attribute of the given type or null if the parameter does not have this attribute defined.  The standard reflection helper GetCustomAttributes will
        /// give you a new instance of the attribute every time you call it.  This helper caches it's results so if you ask for the same attibute twice you will actually
        /// get back the same attribute.  Note that the cache key is based off of the type T that you provide.  So asking for Attr() where T : BaseType> and then asking for Attr() where T : ConcreteType 
        /// will result in two different objects being returned.  If you ask for Attr() where T : BaseType and then Attr() where T :BaseType the caching will work and you'll get the same object back
        /// the second time.
        /// </summary>
        /// <typeparam name="T">The type of attribute to search for</typeparam>
        /// <param name="info">The parameter to inspect</param>
        /// <returns>The desired attribute or null if it is not present</returns>
        public static T Attr<T>(this ParameterInfo info)
        {
            return info.Attrs<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the attributes of the given type.  The standard reflection helper GetCustomAttributes will give you new instances of the attributes every time you call it.  
        /// This helper caches it's results so if you ask for the same attibutes twice you will actually get back the same attributes.  Note that the cache key is based off 
        /// of the type T that you provide.  So asking for Attrs() where T : BaseType and then asking for Attrs() where T : ConcreteType
        /// will result in two different sets of objects being returned.  If you ask for Attrs() where T : BaseType and then Attrs() where T : BaseType the caching will work and you'll get the
        /// same results back the second time.
        /// </summary>
        /// <typeparam name="T">The type of attribute to search for</typeparam>
        /// <param name="info">The parameter to inspect</param>
        /// <returns>The list of attributes that you asked for</returns>
        public static List<T> Attrs<T>(this ParameterInfo info)
        {
            var method = info.Member as MethodInfo;
            if (method == null) throw new ArgumentException("We only support getting attributes for method parameters.");

            var parameters = "(" + string.Join(",", method.GetParameters().Select(p => p.Name).ToArray()) + ")";
            string cacheKey = info.Member.DeclaringType.Name + "." + info.Member.Name + parameters + " - " + info.Name + "(" + info.Position + ")";

            if (cachedAttributes.ContainsKey(cacheKey))
            {
                var cachedValue = cachedAttributes[cacheKey] as List<T>;
                if (cachedValue != null) return cachedValue;
            }

            var freshValue = (from attr in info.GetCustomAttributes(true) where attr.GetType() == typeof(T) || 
                                  attr.GetType().IsSubclassOf(typeof(T)) ||
                                  attr.GetType().GetInterfaces().Contains(typeof(T))
                              select (T)attr).ToList();

            if (cachedAttributes.ContainsKey(cacheKey))
            {
                cachedAttributes[cacheKey] = freshValue;
            }
            else
            {
                cachedAttributes.Add(cacheKey, freshValue);
            }

            return freshValue;
        }
    }
}
