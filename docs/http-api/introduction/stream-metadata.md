# Stream metadata

Every stream in EventStoreDB has metadata stream associated with it, prefixed by `$$`, so the metadata stream from a stream called `foo` is `$$foo`. Internally, the metadata includes information such as the ACL of the stream, the maximum count and age for the events in the stream. Client code can also add information into stream metadata for use with projections or the client API.

Stream metadata is stored internally as JSON, and you can access it over the HTTP API.

## Reading stream metadata

To read the metadata, issue a `GET` request to the attached metadata resource, which is typically of the form:

```http
http://{eventstore-ip-address}/streams/{stream-name}/metadata
```

You should not access metadata by constructing this URL yourself, as the right to change the resource address is reserved. Instead, you should follow the link from the stream itself, which enables your client to tolerate future changes to the addressing structure.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/read-metadata.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/read-metadata.sh)
:::
::::

Once you have the URI of the metadata stream, issue a `GET` request to retrieve the metadata:

```bash
curl -i -H "Accept:application/vnd.eventstore.atom+json" http://127.0.0.1:2113/streams/%24users/metadata --user admin:changeit
```

If you have security enabled, reading metadata may require that you pass credentials, as in the examples above. If credentials are required and you do not pass them, then you receive a `401 Unauthorized` response.

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/missing-credentials.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/missing-credentials.sh)
:::
::::

## Writing metadata

To update the metadata for a stream, issue a `POST` request to the metadata resource.

Inside a file named _metadata.json_:

@[code](@httpapi/metadata.json)

You can also add user-specified metadata here. Some examples user-specified metadata are:

-   Which adapter populates a stream.
-   Which projection created a stream.
-   A correlation ID to a business process.

You then post this information is then posted to the stream:

:::: code-group
::: code-group-item Request
@[code{curl}](@httpapi/update-metadata.sh)
:::
::: code-group-item Response
@[code{response}](@httpapi/update-metadata.sh)
:::
::::

If the specified user does not have permissions to write to the stream metadata, you receive a '401 Unauthorized' response.
