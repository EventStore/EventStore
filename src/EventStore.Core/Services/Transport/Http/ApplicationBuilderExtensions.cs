using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace EventStore.Core.Services.Transport.Http;

public static class ApplicationBuilderExtensions {
	public static IApplicationBuilder UseLogDownloadEndpoint(this IApplicationBuilder app, string logsDir) {
		if (string.IsNullOrEmpty(logsDir)) {
			return app;
		}
		
		return app.Map("/admin/logs", appBuilder => {
			var c = new FileExtensionContentTypeProvider();
			c.Mappings.Clear();
			c.Mappings.Add(".json", "application/json");

			appBuilder
				.UseFileServer(new FileServerOptions() {
					EnableDirectoryBrowsing = true,
					FileProvider = new PhysicalFileProvider(logsDir),
					DirectoryBrowserOptions = {
						Formatter = new JsonDirectoryFormatter(),
						RedirectToAppendTrailingSlash = false,
					},
					StaticFileOptions = {
						ContentTypeProvider = c, // only allow .json files to be served
					},
				});
		});
	}	
}
