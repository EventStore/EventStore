using System.Reflection;

namespace PowerArgs
{
    internal static class ObjectEx
    {
        internal static MethodInfo InvokeMainMethod(this object o)
        {
            var method = o.GetType().GetMethod("Main");
            if (method == null) throw new InvalidArgDefinitionException("There is no Main() method in type " + o.GetType().Name);
            if (method.IsStatic) throw new InvalidArgDefinitionException("The Main() method in type '" + o.GetType().Name + "' must not be static");
            if (method.GetParameters().Length > 0) throw new InvalidArgDefinitionException("The Main() method in type '" + o.GetType().Name + "' must not take any parameters");
            if (method.ReturnType != null && method.ReturnType != typeof(void)) throw new InvalidArgDefinitionException("The Main() method in type '" + o.GetType().Name + "' must return void");

            method.Invoke(o, new object[0]);

            return method;
        }
    }
}
