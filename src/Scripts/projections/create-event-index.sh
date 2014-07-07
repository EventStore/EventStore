#!/bin/bash

curl -i -X POST -d "-" http://127.0.0.1:2113/projections/persistent?name=index-events-by-category\&type=native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath