using System.Collections.Generic;

namespace EventStore.Projections.Core.Messages {
	public sealed class SlaveProjectionCommunicationChannels {
		private readonly Dictionary<string, SlaveProjectionCommunicationChannel[]> _channels;

		public SlaveProjectionCommunicationChannels(
			Dictionary<string, SlaveProjectionCommunicationChannel[]> channels) {
			_channels = channels;
		}

		public Dictionary<string, SlaveProjectionCommunicationChannel[]> Channels {
			get { return _channels; }
		}
	}
}
