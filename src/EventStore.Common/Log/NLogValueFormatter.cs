using System;
using System.Collections;
using System.Text;
using NLog.Config;
using NLog.Internal;
using NLog.MessageTemplates;
using NLog;

namespace EventStore.Common.Log {
	public class NLogValueFormatter : IValueFormatter {
		private IValueFormatter _originalFormatter;
		private bool _isStructured;

		public NLogValueFormatter(IValueFormatter originalFormatter, bool isStructured) {
			_originalFormatter = originalFormatter;
			_isStructured = isStructured;
		}

		public bool FormatValue(object value, string format, CaptureType captureType, IFormatProvider formatProvider,
			StringBuilder builder) {
			if (!_isStructured && captureType == NLog.MessageTemplates.CaptureType.Normal) {
				switch (Convert.GetTypeCode(value)) {
					case TypeCode.String: {
						builder.Append((string)value);
						return true;
					}
					case TypeCode.Char: {
						builder.Append((char)value);
						return true;
					}
					case TypeCode.Empty:
						return true; // null becomes empty string
				}
			}

			return _originalFormatter.FormatValue(value, format, captureType, formatProvider, builder);
		}
	}
}
