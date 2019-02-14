using System;
using System.IO;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;
using HdrHistogram;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class HistogramController : IHttpController {
		private static readonly ICodec[] SupportedCodecs = new ICodec[]
			{Codec.Json, Codec.Xml, Codec.ApplicationXml, Codec.Text};

		public void Subscribe(IHttpService service) {
			Ensure.NotNull(service, "service");
			service.RegisterAction(
				new ControllerAction("/histogram/{name}", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs),
				OnGetHistogram);
		}

		private void OnGetHistogram(HttpEntityManager entity, UriTemplateMatch match) {
			var name = match.BoundVariables["name"];

			var histogram = Histograms.HistogramService.GetHistogram(name);
			if (histogram == null) {
				entity.ReplyStatus(HttpStatusCode.NotFound, "Not found", _ => { });
				return;
			}

			var writer = new StringWriter();
			lock (histogram) {
				histogram.OutputPercentileDistribution(writer, outputValueUnitScalingRatio: 1000.0 * 1000.0);
			}

			var response = Encoding.ASCII.GetBytes(writer.ToString());
			entity.Reply(response,
				HttpStatusCode.OK,
				"OK",
				ContentType.PlainText,
				Encoding.ASCII,
				null,
				_ => { });
		}
	}
}
