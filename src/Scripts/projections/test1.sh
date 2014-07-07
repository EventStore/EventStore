#!/bin/bash

curl -i -X POST -d "-" http://127.0.0.1:2113/projections/persistent?&type=native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath
curl -i -X POST -d "--" http://127.0.0.1:2113/projections/persistent?name=index-events-by-category2\&type=native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath
curl -i -X POST -d "-" http://127.0.0.1:2113/projections/persistent?name=index-events-by-categor
curl -i -X POST -d "-" http://127.0.0.1:2113/projections/persistent?name=index-events-by-categor\&type=native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath
curl -i -X POST -d "-" http://127.0.0.1:2113/projections/persistent?name=index-events-by-category\&type=native:EventStore.Projections.Core.Standard.CategorizeEventsByStreamPath




