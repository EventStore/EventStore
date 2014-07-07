using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PowerArgs
{
    internal class CaseAwareStringComparer : IComparer<string>, IEqualityComparer<string>
    {
        bool ignoreCase;
        public CaseAwareStringComparer(bool ignoreCase)
        {
            this.ignoreCase = ignoreCase;
        }

        public int Compare(string x, string y)
        {
            return string.Compare(x, y, ignoreCase);
        }

        public bool Equals(string x, string y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(string obj)
        {
            return obj.GetHashCode();
        }
    }
}
