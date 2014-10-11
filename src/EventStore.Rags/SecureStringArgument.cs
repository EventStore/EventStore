using System;
using System.Runtime.InteropServices;
using System.Security;
using PowerArgs;

namespace EventStore.Rags
{
    /// <summary>
    /// A PowerArgs argument type that can be used to accept user input without that input appearing on the command line.
    /// It uses secure strings under the hood.
    /// </summary>
    internal class SecureStringArgument
    {
        string name;
        internal SecureStringArgument(string name)
        {
            this.name = name;
        }

        SecureString _secureString;

        /// <summary>
        /// The secure string value.  The first time your code accesses this property is when the user will be presented with
        /// the secure input prompt.
        /// </summary>
        public SecureString SecureString
        {
            get
            {
                if (_secureString != null) return _secureString;

                Console.Write("Enter value for " + name + ": ");
                var ret = new SecureString();
                var index = 0;
                while (true)
                {
                    var key = ConsoleHelper.ConsoleImpl.ReadKey();
                    if (key.Key == ConsoleKey.Enter) break;

                    if (key.Key == ConsoleKey.Backspace && index > 0) ret.RemoveAt(--index);
                    else if (key.Key == ConsoleKey.Delete && index < ret.Length) ret.RemoveAt(index);
                    else switch (key.Key)
                    {
                        case ConsoleKey.LeftArrow:
                            index = Math.Max(0, index - 1);
                            break;
                        case ConsoleKey.RightArrow:
                            index = Math.Min(ret.Length - 1, index++);
                            break;
                        case ConsoleKey.Home:
                            index = 0;
                            break;
                        case ConsoleKey.End:
                            index = ret.Length;
                            break;
                        default:
                            if (char.IsLetterOrDigit(key.KeyChar) || key.KeyChar == ' ' || char.IsSymbol(key.KeyChar) || char.IsPunctuation(key.KeyChar))
                            {
                                if (index < ret.Length)
                                {
                                    ret.InsertAt(index, key.KeyChar);
                                }
                                else
                                {
                                    ret.AppendChar(key.KeyChar);
                                }
                                index++;
                            }
                            break;
                    }
                }

                ret.MakeReadOnly();
                _secureString = ret;
                return ret;
            }
        }

        /// <summary>
        /// Converts the underlying secure string to a regular string.
        /// </summary>
        /// <returns>A normal string representation of the user's input.</returns>
        public string ConvertToNonsecureString()
        {
            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(SecureString);
                return Marshal.PtrToStringUni(unmanagedString);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}