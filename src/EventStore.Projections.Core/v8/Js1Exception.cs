using System;

namespace EventStore.Projections.Core.v8 {
	public class Js1Exception : Exception {
		private readonly int _errorCode;

		public Js1Exception(int errorCode, string errorMessage)
			: base(errorMessage) {
			_errorCode = errorCode;
		}

		public int ErrorCode {
			get { return _errorCode; }
		}
	}
}
