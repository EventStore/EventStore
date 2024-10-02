# Remove ProjectionExecutionTimeout settings

This script removes the persisted projection-specific `ProjectionExecutionTimeout` setting on all non-transient projections in EventStoreDB, allowing the projection to fall back to the default value configured by the database `ProjectionExecutionTimeout` setting.

This forms part of the fix introduced in [PR#4423](https://github.com/EventStore/EventStore/pull/4423).

## Usage

Example usage for a local test instance:

```
./RemoveProjectionExecutionTimeout.ps1 -EventStoreAddress "https://localhost:2113" -AdminUsername "admin" -AdminPassword (ConvertTo-SecureString "changeit" -AsPlainText)
```

### Parameters

| Name                  | Type              | Description |
|-----------------------|-------------------|-------------|
| EventStoreAddress     | `String`          | The base address of the EventStoreDB server.  |
| AdminUsername         | `String`          | The username of a user with admin privileges. |
| AdminPassword         | `SecureString`    | The password for the admin user.              |
