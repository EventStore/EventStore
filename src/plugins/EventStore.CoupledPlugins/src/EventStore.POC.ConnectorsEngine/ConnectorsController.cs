// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EventStore.POC.ConnectorsEngine.Infrastructure;
using EventStore.POC.ConnectorsEngine.Processing;
using EventStore.POC.IO.Core.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EventStore.POC.ConnectorsEngine;

// Internal Hosting
//   Create a connector:
//     curl --insecure --fail -d@connector.json -X POST "https://admin:changeit@localhost:2113/connectors/console-one"
//   List all connectors:
//     curl --insecure --fail -X GET https://admin:changeit@localhost:2113/connectors/list
//
//  Send all to console sink: https://admin:changeit@localhost:2113/connectors/console-one
//    { "Sink": "console://" }
//
//  JsonPath Filter for eventType = someEventType: https://admin:changeit@localhost:2113/connectors/console-two
//    { "Sink": "console://", "Filter": "$[?($.eventType=='someEventType')]" }
//
//  JsonPath Filter for {"test":"test"} as event data: https://admin:changeit@localhost:2113/connectors/console-three
//    { "Sink": "console://", "Filter": "$[?($.data.test=='test')]" }
//
//   External Receivers by SampleReceiver
//    https://admin:changeit@localhost:2113/connectors/http-one: { "Sink": "http://localhost:8080/receivers/connector-one" }
//    https://admin:changeit@localhost:2113/connectors/http-two: { "Sink": "http://localhost:8080/receivers/connector-two" }
//
// External Hosting by ConnectedSubsystemHost & SampleReceiver
//   Create a connector:
//     curl --insecure --fail -d@connector.json -X POST "http://admin:changeit@localhost:2119/connectors/console-one"
//   List all connectors:
//     curl --insecure --fail -X GET "http://admin:changeit@localhost:2119/connectors/list"
//	Enable a connector:
//     curl --insecure --fail -X POST "http://admin:changeit@localhost:2119/connectors/{connectorId}/enable"
//  Disable a connector:
//	   curl --insecure --fail -X POST "http://admin:changeit@localhost:2119/connectors/{connectorId}/disable"
//  Delete a connector:
//	   curl --insecure --fail -X DELETE "http://admin:changeit@localhost:2119/connectors/{connectorId}"
//  Reset a connector:
//	   curl --insecure --fail -X POST "http://admin:changeit@localhost:2119/connectors/{connectorId}/reset"

//   View PNG Image Example
//     POST Binary Data from repository root
//       curl --insecure --fail -X POST --data-binary "@ouro.png" "https://admin:changeit@localhost:2113/streams/png" -H "ES-EventType: png" -H "Content-Type: application/octet-stream" -H "ES-EventId: d6cee325-d5b7-46ea-961b-d165d5cf0c96"
//     Create Connector
//       curl --insecure --fail -X POST -d@connector.json "http://admin:changeit@localhost:2119/connectors/http-bin-one"
//  { "Sink": "http://localhost:8080/receivers/binary", "Filter": "$[?($.stream=='png'&&$.eventType=='png')]" }
//     View Image in Browser with
//       http://localhost:8080/receivers/binary
//

[ApiController]
[Route("connectors")]
[Authorize(Roles = "$admins")]
public class ConnectorsController : ControllerBase {
	private static readonly JsonSerializerOptions _jsonOptions = new() {
		Converters = {
			new EnumConverterWithDefault<NodeState>(),
		},
		PropertyNameCaseInsensitive = true,
		UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
	};

	private readonly ILogger<ConnectorsController> _logger;
	private readonly Engine _engine;

	public ConnectorsController(ILogger<ConnectorsController> logger, Engine engine) {
		_logger = logger;
		_engine = engine;
	}

	[HttpPost]
	[Route("{connectorId}/enable")]
	public async Task<IActionResult> Enable(string connectorId, CancellationToken ct) {
		try {
			await _engine.WriteSide.Enable(connectorId, ct);
			return Ok();
		} catch (CommandException ex) {
			return BadRequest(ex.Message);
		}
	}

	[HttpPost]
	[Route("{connectorId}/disable")]
	public async Task<IActionResult> Disable(string connectorId, CancellationToken ct) {
		try {
			await _engine.WriteSide.Disable(connectorId, ct);
			return Ok();
		} catch (CommandException ex) {
			return BadRequest(ex.Message);
		}
	}

	[HttpPost]
	[Route("{connectorId}/reset")]
	public async Task<IActionResult> Reset(
		string connectorId,
		[FromBody] JsonNode json,
		CancellationToken ct) {

		Commands.ResetConnector? cmd;
		try {
			json["ConnectorId"] = connectorId;
			cmd = json.Deserialize<Commands.ResetConnector>(_jsonOptions);
			if (cmd is null) {
				return BadRequest($"Could not parse reset connector command");
			}
		} catch (JsonException ex) {
			return BadRequest(ex.Message);
		}

		try {
			await _engine.WriteSide.Reset(cmd, ct);
			return Ok();
		} catch (CommandException ex) {
			return BadRequest(ex.Message);
		}
	}

	[HttpDelete]
	[Route("{connectorId}")]
	public async Task<IActionResult> Delete(string connectorId, CancellationToken ct) {
		try {
			await _engine.WriteSide.Delete(connectorId, ct);
			return Ok();
		} catch (CommandException ex) {
			return BadRequest(ex.Message);
		}
	}

	[HttpPost]
	[Route("{connectorId}")]
	public async Task<IActionResult> Create(string connectorId, [FromBody]JsonNode json, CancellationToken ct) {
		Commands.CreateConnector? cmd;
		try {
			json["ConnectorId"] = connectorId;
			cmd = json.Deserialize<Commands.CreateConnector>(_jsonOptions);
			if (cmd is null) {
				return BadRequest($"Could not parse Connector configuration");
			}
		} catch (JsonException ex) {
			return BadRequest(ex.Message);
		}

		try {
			await _engine.WriteSide.Create(cmd, ct);
			return Ok();
		} catch (CommandException ex) {
			return BadRequest(ex.Message);
		}
	}

	[HttpGet]
	[Route("list")]
	public async Task<IEnumerable<ConnectorState>> List() {
		return await _engine.ReadSide.ListAsync();
	}

	[HttpGet]
	[Route("activeOnNode")]
	public async Task<IEnumerable<string>> ActiveOnNode() {
		return await _engine.ReadSide.ListActiveConnectorsAsync();
	}
}
