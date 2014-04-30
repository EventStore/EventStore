using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PowerArgs
{
    internal static class FieldInfoEx
    {
        internal static List<string> GetEnumShortcuts(this FieldInfo enumField)
        {
            if (enumField.DeclaringType.IsEnum == false) throw new ArgumentException("The given field '" + enumField.Name + "' is not an enum field.");

            var shortcutAttrs = enumField.Attrs<ArgShortcut>();
            var noShortcutPolicy = shortcutAttrs.Where(s => s.Shortcut == null).SingleOrDefault();
            var shortcutVals = shortcutAttrs.Where(s => s.Shortcut != null).Select(s => s.Shortcut).ToList();

            if (noShortcutPolicy != null && shortcutVals.Count > 0) throw new InvalidArgDefinitionException("You can't have an ArgShortcut attribute with a null shortcut and then define a second ArgShortcut attribute with a non-null value.");

            return shortcutVals;
        }
    }
}
