using System.Text;
using NLog;
using NLog.LayoutRenderers;

namespace EventStore.Common.Log {
	[LayoutRenderer("logsdir")]
	public class NLogDirectoryLayoutRendered : LayoutRenderer {
		protected override void Append(StringBuilder builder, LogEventInfo logEvent) {
			builder.Append(LogManager._logsDirectory);
		}
	}
}
