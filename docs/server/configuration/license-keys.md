---
order: 2
title: EventStore Licensing
---

# EventStore Licensing

Some features of EventStoreDB require a license key to access.

The license key can be provided to EventStoreDB via environment variable or config file in the [usual plugin config location](./plugins.md#json-files).

Environment variable:

```
EventStore__Plugins__Licensing__LicenseKey={Your key}
```

Configuration file:

```
{
	"EventStore": {
		"Plugins": {
			"Licensing": {
				"LicenseKey": "Your key"
			}
		}
	}
}
```

EventStoreDB will not start if features are enabled that require a license key but the license is not provided or is invalid.