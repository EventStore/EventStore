using System;

namespace PowerArgs
{
    /// <summary>
    /// Used for internal implementation, but marked public for testing, please do not use.
    /// </summary>
    public interface IConsoleProvider
    {
        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        int CursorLeft { get; set; }

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        int CursorTop { get; set; }

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        int BufferWidth { get; }

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        ConsoleKeyInfo ReadKey();

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        void Write(object output);

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        void WriteLine(object output);

        /// <summary>
        /// Used for internal implementation, but marked public for testing, please do not use.
        /// </summary>
        void WriteLine();

        /// <summary>
        /// Clears the console window
        /// </summary>
        void Clear();
    }
}
