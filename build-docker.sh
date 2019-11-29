#!/usr/bin/env bash
mkdir testresults

docker build \
  --target test \
  --tag eventstore-test \
  --build-arg RUNTIME=$(RUNTIME) \
  --build-arg CONTAINER_RUNTIME=$(CONTAINER_RUNTIME) \
  .

docker build \
  --tag eventstore \
  --build-arg RUNTIME=$(RUNTIME) \
  --build-arg CONTAINER_RUNTIME=$(CONTAINER_RUNTIME) \
  .

docker run --name tests eventstore-test
EXIT_CODE=$?
docker cp tests:/build/testresults ./
exit $EXIT_CODE

