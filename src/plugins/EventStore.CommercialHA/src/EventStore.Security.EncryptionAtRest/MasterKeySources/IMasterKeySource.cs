// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;

namespace EventStore.Security.EncryptionAtRest.MasterKeySources;

public interface IMasterKeySource {
	public string Name { get; }

	// the interface supports loading multiple master keys to cater for scenarios where a master key is compromised:
	// a new master key with a new, higher, ID can be generated to encrypt new data. however, old data must still be
	// decrypted using the old master key(s). the master key with the highest ID is always the active one.
	// master key sources are responsible for sorting the resulting list of master keys by increasing ID.
	public IReadOnlyList<MasterKey> LoadMasterKeys();
}
