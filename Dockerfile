# "build" image
ARG CONTAINER_RUNTIME=jammy
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
ARG RUNTIME=linux-x64

WORKDIR /build/ci
COPY ./ci ./

WORKDIR /build/docs
COPY ./docs ./

WORKDIR /build/src
COPY ./src/EventStore.sln ./src/*/*.csproj ./src/Directory.Build.* ./
RUN for file in $(ls *.csproj); do mkdir -p ./${file%.*}/ && mv $file ./${file%.*}/; done
RUN dotnet restore --runtime=${RUNTIME}
COPY ./src .

WORKDIR /build/.git
COPY ./.git .

WORKDIR /build/src
RUN find /build/src -maxdepth 1 -type d -name "*.Tests" -print0 | xargs -I{} -0 -n1 sh -c \
    'dotnet publish --runtime=${RUNTIME} --no-self-contained --configuration Release --output /build/published-tests/`basename $1` $1' - '{}'

# "test" image
FROM mcr.microsoft.com/dotnet/sdk:8.0-${CONTAINER_RUNTIME} as test
WORKDIR /build
COPY --from=build ./build/published-tests ./published-tests
COPY --from=build ./build/ci ./ci
COPY --from=build ./build/src/EventStore.Core.Tests/Services/Transport/Tcp/test_certificates/ca/ca.crt /usr/local/share/ca-certificates/ca_eventstore_test.crt
RUN mkdir ./test-results
RUN printf '#!/usr/bin/env sh\n\
update-ca-certificates\n\
find /build/published-tests -maxdepth 1 -type d -name "*.Tests" -print0 | xargs -I{} -0 -n1 sh -c '"'"'proj=`basename $1` && dotnet test --blame --blame-hang-timeout 5min --settings /build/ci/ci.runsettings --logger:"GitHubActions;report-warnings=false" --logger:html --logger:trx --logger:"console;verbosity=normal" --results-directory /build/test-results/$proj $1/$proj.dll'"'"' - '"'"'{}'"'"'\n\
exit_code=$?\n\
echo $(find /build/test-results -name "*.html" | xargs cat) > /build/test-results/test-results.html\n\
exit $exit_code' \
    >> /build/test.sh && \
    chmod +x /build/test.sh

CMD ["/build/test.sh"]

# "publish" image
FROM build as publish
ARG RUNTIME=linux-x64

RUN dotnet publish --configuration=Release --runtime=${RUNTIME} --self-contained \
     --framework=net8.0 --output /publish EventStore.ClusterNode

# "runtime" image
FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-${CONTAINER_RUNTIME} AS runtime
ARG RUNTIME=linux-x64
ARG UID=1000
ARG GID=1000

RUN if [[ "${RUNTIME}" = "linux-musl-x64" ]];\
    then \
        apk update && \
        apk add --no-cache \
        curl; \
    else \
        apt update && \
        apt install -y \
        curl && \
        rm -rf /var/lib/apt/lists/*; \
    fi

WORKDIR /opt/eventstore

RUN addgroup --gid ${GID} "eventstore" && \
    adduser \
    --disabled-password \
    --gecos "" \
    --ingroup "eventstore" \
    --no-create-home \
    --uid ${UID} \
    "eventstore"

COPY --chown=eventstore:eventstore --from=publish /publish ./

RUN mkdir -p /var/lib/eventstore && \
    mkdir -p /var/log/eventstore && \
    mkdir -p /etc/eventstore && \
    chown -R eventstore:eventstore /var/lib/eventstore /var/log/eventstore /etc/eventstore

USER eventstore

RUN printf "NodeIp: 0.0.0.0\n\
ReplicationIp: 0.0.0.0" >> /etc/eventstore/eventstore.conf

VOLUME /var/lib/eventstore /var/log/eventstore

EXPOSE 1112/tcp 1113/tcp 2113/tcp

HEALTHCHECK --interval=5s --timeout=5s --retries=24 \
    CMD curl --fail --insecure https://localhost:2113/health/live || curl --fail http://localhost:2113/health/live || exit 1

ENTRYPOINT ["/opt/eventstore/EventStore.ClusterNode"]
