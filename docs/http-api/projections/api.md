# Projections API

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projections/any` | Returns all known projections. | GET
`/projections/all-non-transient` | Returns all known non ad-hoc projections. | GET
`/projections/transient` | Returns all known ad-hoc projections. | GET
`/projections/onetime`| Returns all known one-time projections. | GET
`/projections/continuous` | Returns all known continuous projections. | GET
`/projections/transient?name={name}&type={type}&enabled={enabled}` | Create an ad-hoc projection. This type of projection runs until completion and automatically deleted afterwards. | POST

### Parameters

* name: Name of the projection

* type: JS or Native. (JavaScript or native. At this time, EventStoreDB only supports JavaScript)

* enabled: Enable the projection (true/false)

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projections/onetime?name={name}&type={type}&enabled={enabled}&checkpoints={checkpoints}&emit={emit}&trackemittedstreams={trackemittedstreams}` | Create a one-time projection. This type of projection runs until completion and then stops. | POST

### Parameters

* name: Name of the projection

* type: JS or Native. (JavaScript or native. At this time, EventStoreDB only supports JavaScript)

* enabled: Enable the projection (true/false)

* checkpoints: Enable checkpoints (true/false)

* emit: Enable the ability for the projection to append to streams (true/false)

* trackemittedstreams: Write the name of the streams the projection is managing to a separate stream. $projections-{projection-name}-emittedstreams (true/false)

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projections/continuous?name={name}&type={type}&enabled={enabled}&emit={emit}&trackemittedstreams={trackemittedstreams}` | Create a continuous projection. This type of projection will, if enabled will continuously run unless disabled or an unrecoverable error is encountered. | POST

### Parameters

* name: Name of the projection

* type: JS or Native. (JavaScript or native. At this time, EventStoreDB only supports JavaScript)

* enabled: Enable the projection (true/false)

* emit: Allow the projection to append to streams (true/false)

* trackemittedstreams: Write the name of the streams the projection is managing to a separate stream. $projections-{projection-name}-emittedstreams (true/false)

`/projection/{name}/query?config={config}` | Returns the definition query and if config is set to true, will return the configuration. | GET

### Parameters

* name: Name of the projection

* config: Return the definition of the projection (true/false)

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/query?type={type}&emit={emit}`| Update a projection's query. | PUT

### Parameters

* name: Name of the projection

* type: JS or Native. (JavaScript or native. At this time, EventStoreDB only supports JavaScript)

* emit: Allow the projection to write to streams (true/false)

* trackemittedstreams: Write the name of the streams the projection is managing to a separate stream. $projections-{projection-name}-emittedstreams (true/false)

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}` | Returns information for a projection. | GET

`/projection/{name}?deleteStateStream={deleteStateStream}&deleteCheckpointStream={deleteCheckpointStream}&deleteEmittedStreams={deleteEmittedStreams}` | Delete a projection, optionally delete the streams that were created as part of the projection. | DELETE

### Parameters

* name: Name of the projection

* deleteStateStream: Delete the state stream (true/false)

* deleteCheckpointStream: Delete the checkpoint stream (true/false)

* deleteEmittedStreams: Delete the emitted streams stream (true/false)

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/statistics` | Returns detailed information for a projection. | GET

### Parameters

* name: Name of the projection

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/state?partition={partition}` | Query for the state of a projection. | GET

### Parameters

* name: Name of the projection

* partition: The partition

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/result?partition={partition}` | Query for the result of a projection. | GET

### Parameters

* name: Name of the projection

* partition: The partition

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/command/disable?enableRunAs={enableRunAs}` | Disable a projection. | POST

### Parameters

* name: Name of the projection

* enableRunAs: Enables the projection to run as the user who issued the request.

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/command/enable?enableRunAs={enableRunAs}` | Enable a projection. | POST

### Parameters

* name: Name of the projection

* enableRunAs: Enables the projection to run as the user who issued the request.

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/command/reset?enableRunAs={enableRunAs}` | Reset a projection. (This will re-emit events, streams that are written to from the projection will also be soft deleted). | POST

### Parameters

* name: Name of the projection

* enableRunAs: Enables the projection to run as the user who issued the request.

URI | Description | HTTP Verb
|:-------|:------------|:----
`/projection/{name}/command/abort?enableRunAs={enableRunAs}` | Abort a projection. | POST

### Parameters

* name: Name of the projection

* enableRunAs: Enables the projection to run as the user who issued th 
