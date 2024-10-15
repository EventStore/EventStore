// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using EventStore.Licensing.Keygen;

namespace EventStore.Licensing.Tests.Keygen;

partial class KeygenSimulator {
	public async Task ReplyWith_ValidationResponse(string code) => await Send(
		HttpStatusCode.OK,
		new Models.ValidateLicenseResponse {
			Data = new() {
				Attributes = new() {
					Name = "the name of the license",
					Metadata = new() {
						{ "trial", "false" }
					}
				},
				Id = "the-license-id"
			},
			Meta = new() {
				Code = code,
				Detail = code.ToLower(),
				Valid = code == "VALID",
			},
		});

	public async Task ReplyWith_Error(string code) => await Send(
		HttpStatusCode.Forbidden,
		new Models.KeygenResponse<Models.KeygenAttributes, object, object> {
			Errors = [
				new() {
					Title = code.ToLower(),
					Code = code,
				}
			],
		});

	public async Task ReplyWith_Entitlements(params string[] entitlements) => await Send(
		HttpStatusCode.OK,
		new Models.EntitlementsResponse {
			Data = entitlements.Select(x =>
				new Models.KeygenData<Models.EntitlementAttributes, object>() {
					Attributes = new() {
						Name = x.ToLower(),
						Code = x,
						Metadata = [],
					}
				}
			).ToArray(),
		});

	public async Task ReplyWith_Machine() => await Send(
		HttpStatusCode.OK,
		new Models.GetMachineResponse {
			Data = new() {
				Attributes = new() {
					RequireHeartbeat = true,
					HeartbeatDuration = 1,
				}
			}
		});

	public async Task ReplyWith_HeartbeatResponse() => await Send(
		HttpStatusCode.OK,
		new Models.HeartbeatResponse {
			Data = new() {
				Attributes = new() {
					HeartbeatStatus = "ALIVE",
				}
			}
		});

	public async Task ReplyWith_ActivationSuccess() => await Send(
		HttpStatusCode.OK,
		new Models.ActivateMachineResponse {
			Data = new(),
		});

	public async Task ReplyWith_ActivationError(string code) => await Send(
		HttpStatusCode.UnprocessableContent,
		new Models.ActivateMachineResponse {
			Errors = [
				new() {
					Title = code.ToLower(),
					Code = code,
				}
			],
		});

	public async Task ReplyWith_DeactivationSuccess() => await Send(
		HttpStatusCode.NoContent, new { });

	public async Task ReplyWith_DeactivationError() => await Send(
		HttpStatusCode.NotFound, new { });
}
