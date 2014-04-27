using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PowerArgs
{
    internal static class TypeEx
    {
        internal static List<string> GetEnumShortcuts(this Type enumType)
        {
            List<string> ret = new List<string>();
            foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false))
            {
                ret.AddRange(field.GetEnumShortcuts());
            }
            return ret;
        }

        internal static bool TryMatchEnumShortcut(this Type enumType, string value, bool ignoreCase, out object enumResult)
        {
            if (ignoreCase) value = value.ToLower();
            foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false))
            {
                var shortcuts = field.GetEnumShortcuts();
                if (ignoreCase) shortcuts = shortcuts.Select(s => s.ToLower()).ToList();
                var match = (from s in shortcuts where s == value select s).SingleOrDefault();
                if (match != null)
                {
                    enumResult = Enum.Parse(enumType, field.Name);
                    return true;
                }
            }

            enumResult = null;
            return false;
        }

        internal static void ValidateNoDuplicateEnumShortcuts(this Type enumType, bool ignoreCase)
        {
            if (enumType.IsEnum == false) throw new ArgumentException("Type " + enumType.Name + " is not an enum");

            List<string> shortcutsSeenSoFar = new List<string>();
            foreach (var field in enumType.GetFields().Where(f => f.IsSpecialName == false))
            {
                var shortcutsForThisField = field.GetEnumShortcuts();
                if (ignoreCase) shortcutsForThisField = shortcutsForThisField.Select(s => s.ToLower()).ToList();

                foreach (var shortcut in shortcutsForThisField)
                {
                    if (shortcutsSeenSoFar.Contains(shortcut)) throw new InvalidArgDefinitionException("Duplicate shortcuts defined for enum type '" + enumType.Name + "'");
                    shortcutsSeenSoFar.Add(shortcut);
                }
            }
        }

        internal static List<MethodInfo> GetActionMethods(this Type t)
        {
            if (t.HasAttr<ArgActionType>())
            {
                t = t.Attr<ArgActionType>().ActionType;
            }

            return (from m in t.GetMethods()
                    where m.HasAttr<ArgActionMethod>()
                    select m).ToList();
        }
    }
}
