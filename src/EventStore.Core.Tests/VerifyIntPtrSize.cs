using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace EventStore.Core.Tests {
	[TestFixture]
	public class VerifyIntPtrSize {
		[Test]
		public void TestIntPtrSize() {
			Assert.AreEqual(8, IntPtr.Size);
		}
	}

	public static class WebHostBuilderExtensions {
		public static IWebHostBuilder UseStartup(this IWebHostBuilder builder, IStartup startup)
			=> builder
				.ConfigureServices(services => services.AddSingleton(startup));
	}
}
