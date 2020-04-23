
namespace System.ServiceModel {
	internal sealed class DiagnosticUtility {
		internal sealed class ExceptionUtility {
			internal static Exception ThrowHelperArgumentNull(string paramName) {
				throw new ArgumentNullException(paramName);
			}

			internal static Exception ThrowHelperArgument(string paramName, string message) {
				throw new ArgumentException(message, paramName);
			}

			internal static Exception ThrowHelperError(Exception exception) {
				throw exception;
			}
		}
	}
}
