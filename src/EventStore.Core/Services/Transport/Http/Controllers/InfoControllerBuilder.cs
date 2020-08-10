using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Plugins.Authentication;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class InfoControllerBuilder {
		private IOptions _options;
		private IDictionary<string, bool> _features;
		private IAuthenticationProvider _authenticationProvider;

		public InfoControllerBuilder WithOptions(IOptions options) {
			_options = options;
			return this;

		}

		public InfoControllerBuilder WithFeatures(IDictionary<string, bool> features) {
			_features = features;
			return this;
		}

		public InfoControllerBuilder WithAuthenticationProvider(IAuthenticationProvider authenticationProvider) {
			_authenticationProvider = authenticationProvider;
			return this;
		}

		public InfoController Build() {
			return new InfoController(_options, _features, _authenticationProvider);
		}
	}
}
