// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

Console.WriteLine("Running receivers...");

var builder = WebApplication.CreateBuilder();
var app = builder.Build();
var pngStream = new MemoryStream();

app.MapPost("/receivers/connector-one", async (HttpRequest req, HttpResponse resp) => {

	var t = new HttpRequestStreamReader(req.Body, Encoding.UTF8);
	var s = await t.ReadToEndAsync();

	// communicate back to sink
	resp.StatusCode = 200;
	await resp.WriteAsync("Thanks!");

	Console.WriteLine($"SampleReceiver: Received > connector-one: {s}");
});

app.MapPost("/receivers/connector-two", async (HttpRequest req, HttpResponse resp) => {

	using var t = new HttpRequestStreamReader(req.Body, Encoding.UTF8);
	var s = await t.ReadToEndAsync();

	// communicate back to sink
	resp.StatusCode = 200;
	await resp.WriteAsync("Thanks!");
	
	Console.WriteLine($"SampleReceiver: Received > connector-two: {s}");
});

app.MapPost("/status-500", async (HttpRequest req, HttpResponse resp) => {

	var t = new HttpRequestStreamReader(req.Body, Encoding.UTF8);
	var s = await t.ReadToEndAsync();

	// communicate back to sink
	resp.StatusCode = 500;
	await resp.WriteAsync("Thanks!");
});

app.MapPost("/receivers/json", ([FromBody] JsonNode json) => {

	Console.WriteLine($"Receiver: JSON POST: JSON: {json}");
});

app.MapPost("/receivers/binary", async (HttpRequest req) => {

	Console.WriteLine($"Receiver: Binary POST: length={req.ContentLength}");

	if (pngStream.Length == 0) {
		await req.BodyReader.CopyToAsync(pngStream);			
	}
});

app.MapGet("/receivers/binary", () => {

	Console.WriteLine("Receiver: Binary GET");

	var response = new MemoryStream(
		buffer: pngStream.GetBuffer(),
		index: 0,
		count: (int)pngStream.Length);
		
	return Results.Stream(response, "image/png");
});

await app.RunAsync("http://localhost:8080");
