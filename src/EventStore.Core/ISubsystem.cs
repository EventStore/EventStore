using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace EventStore.Core {
	public interface ISubsystem {
		void Register(StandardComponents standardComponents);

		IEnumerable<Task> Start();
		void Stop();

		Func<IApplicationBuilder, IApplicationBuilder> Configure { get; }
		Func<IServiceCollection, IServiceCollection> ConfigureServices { get; }
	}
}
