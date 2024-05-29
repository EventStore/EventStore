using System;

namespace EventStore.Core.Services.Storage.Scavenge;

public record AutoScavengeProcessCompleted(DateTime Ended);
