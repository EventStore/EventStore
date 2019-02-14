using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Simple abstraction of a logger.
	/// </summary>
	/// <remarks>
	/// You can pass your own logging abstractions into the Event Store Client API. Just pass 
	/// in your own implementation of <see cref="ILogger"/> when constructing your client connection.
	/// </remarks>
	public interface ILogger {
		/// <summary>
		/// Writes an error to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Error(string format, params object[] args);

		/// <summary>
		/// Writes an error to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Error(Exception ex, string format, params object[] args);

		/// <summary>
		/// Writes an information message to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Info(string format, params object[] args);

		/// <summary>
		/// Writes an information message to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Info(Exception ex, string format, params object[] args);

		/// <summary>
		/// Writes a debug message to the logger
		/// </summary>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Debug(string format, params object[] args);

		/// <summary>
		/// Writes a debug message to the logger
		/// </summary>
		/// <param name="ex">A thrown exception.</param>
		/// <param name="format">Format string for the log message.</param>
		/// <param name="args">Arguments to be inserted into the format string.</param>
		void Debug(Exception ex, string format, params object[] args);
	}
}
