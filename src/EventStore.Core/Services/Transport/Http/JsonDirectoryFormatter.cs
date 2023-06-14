using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace EventStore.Core.Services.Transport.Http;

public class JsonDirectoryFormatter : IDirectoryFormatter {
	// Implementation based on: https://github.com/dotnet/aspnetcore/blob/main/src/Middleware/StaticFiles/src/HtmlDirectoryFormatter.cs
	private const string JsonUtf8 = "application/json; charset=utf-8";
	private const string JsonExt = ".json";

	public Task GenerateContentAsync(HttpContext context, IEnumerable<IFileInfo> contents) {
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(contents);
			
		context.Response.ContentType = JsonUtf8;
			
		if (HttpMethods.IsHead(context.Request.Method))
		{
			return Task.CompletedTask;
		}
			
		using var m = new MemoryStream();
		using var w = new Utf8JsonWriter(m);
		w.WriteStartArray();
			
		foreach (var fileInfo in contents.Where(c => !c.IsDirectory && c.Name.EndsWith(JsonExt))) {
			w.WriteStartObject();
			w.WriteString("name", fileInfo.Name);
			w.WriteString("lastModified",  fileInfo.LastModified.ToString("O"));
			w.WriteNumber("size", fileInfo.Length);
			w.WriteEndObject();
		}
			
		w.WriteEndArray();
		w.Flush();

		var bytes = m.GetBuffer();
			
		return context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
	}
}
