#!/bin/bash

curl -i -X POST -d '

{
	"CorrelationId" : "0f7fac5b-d9cb-469f-a167-70867728950e",
	"ExpectedVersion" : "-1",
	"Events" : [
			{
				"EventId" : "0f9fad5b-d9cb-469f-a165-70867728951e",
   				"EventType" : "Type",
				"Data" : { "Password" : "123" }
			}
		   ]
}
' http://127.0.0.1:2113/streams/\$user-z 

