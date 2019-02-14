using System;

namespace EventStore.Rags {
	/// <summary>
	/// An exception that should be thrown when the error condition is caused because of bad user input.
	/// </summary>
	public class ArgException : Exception {
		/// <summary>
		/// The parser context that may be incomplete since it depends on where the exception was thrown
		/// </summary>
		//TODO GFY WILL WE USE HOOK CONTEXT?
		// public ArgHook.HookContext Context { get; internal set; }

		/// <summary>
		/// Creates a new ArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public ArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new ArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public ArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when the error condition is caused by an improperly formed
	/// argument scaffold type.  For example if the user specified the same shortcut value for more than one property.
	/// </summary>
	public class InvalidArgDefinitionException : Exception {
		/// <summary>
		/// Creates a new InvalidArgDefinitionException given a message.
		/// </summary>
		/// <param name="msg">An error message.</param>
		public InvalidArgDefinitionException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new InvalidArgDefinitionException given a message.
		/// </summary>
		/// <param name="msg">An error message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public InvalidArgDefinitionException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when an unexpected named|positional argument is found.
	/// </summary>
	public class UnexpectedArgException : ArgException {
		/// <summary>
		/// Creates a new UnexpectedArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public UnexpectedArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new UnexpectedArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public UnexpectedArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when the same argument is repeated.
	/// </summary>
	public class DuplicateArgException : ArgException {
		/// <summary>
		/// Creates a new DuplicateArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public DuplicateArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new DuplicateArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public DuplicateArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when a required argument is missing.
	/// </summary>
	public class MissingArgException : ArgException {
		/// <summary>
		/// Creates a new MissingArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public MissingArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new MissingArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public MissingArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when an unknown action argument is specified.
	/// </summary>
	public class UnknownActionArgException : ArgException {
		/// <summary>
		/// Creates a new UnknownActionArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public UnknownActionArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new UnknownActionArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public UnknownActionArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when the query can not be compiled.
	/// </summary>
	public class QueryInvalidArgException : ArgException {
		/// <summary>
		/// Creates a new QueryInvalidArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public QueryInvalidArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new QueryInvalidArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public QueryInvalidArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	/// <summary>
	/// An exception that should be thrown when an argument's value is not valid.
	/// </summary>
	public class ValidationArgException : ArgException {
		/// <summary>
		/// Creates a new ValidationArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		public ValidationArgException(string msg) : base(msg) {
		}

		/// <summary>
		/// Creates a new ValidationArgException given a user friendly message
		/// </summary>
		/// <param name="msg">A user friendly message.</param>
		/// <param name="inner">The inner exception that caused the problem</param>
		public ValidationArgException(string msg, Exception inner) : base(msg, inner) {
		}
	}

	internal class ArgCancelProcessingException : Exception {
	}
}
