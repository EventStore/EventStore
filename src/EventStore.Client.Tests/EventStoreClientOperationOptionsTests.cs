using System;
using Xunit;

namespace EventStore.Client {
	public class EventStoreClientOperationOptionsTests {
		[Fact]
		public void setting_options_on_clone_should_not_modify_original() {
			EventStoreClientOperationOptions options = new EventStoreClientOperationOptions {
				TimeoutAfter = TimeSpan.FromDays(5),
			};
			
			var clonedOptions = options.Clone();
			clonedOptions.TimeoutAfter = TimeSpan.FromSeconds(1);
			
			Assert.Equal(options.TimeoutAfter, TimeSpan.FromDays(5));
			Assert.Equal(clonedOptions.TimeoutAfter, TimeSpan.FromSeconds(1));
		}
	}
}
