ARG CONTAINER_RUNTIME=focal
FROM mcr.microsoft.com/dotnet/sdk:5.0-focal AS build
ARG RUNTIME=linux-x64

WORKDIR /build/ci

COPY ./ci ./

WORKDIR /build/src

COPY ./src/EventStore.sln ./src/*/*.csproj ./src/Directory.Build.* ./

RUN for file in $(ls *.csproj); do mkdir -p ./${file%.*}/ && mv $file ./${file%.*}/; done

RUN dotnet restore --runtime=${RUNTIME}

COPY ./src .

WORKDIR /build/.git

COPY ./.git .

WORKDIR /build/src

RUN dotnet build --configuration=Release --no-restore

FROM build as test
ARG RUNTIME=linux-x64
RUN echo '#!/usr/bin/env sh\n\
cp /build/src/EventStore.Core.Tests/Services/Transport/Tcp/test_certificates/ca/ca.pem /usr/local/share/ca-certificates/ca_eventstore_test.crt\n\
update-ca-certificates\n\
find /build/src -maxdepth 1 -type d -name "*.Tests" -print0 | xargs -I{} -0 -n1 bash -c '"'"'dotnet test --runtime=${RUNTIME} --configuration Release --blame --settings /build/ci/ci.runsettings --logger:"GitHubActions;report-warnings=false" --logger:html --logger:trx --logger:"console;verbosity=normal" --results-directory=/build/test-results/$1 $1'"'"' - '"'"'{}'"'"'\n\
exit_code=$?\n\
echo $(find /build/test-results -name "*.html" | xargs cat) > /build/test-results/test-results.html\n\
exit $exit_code' \
    >> /build/test.sh && \
    chmod +x /build/test.sh
CMD ["/build/test.sh"]

FROM build as publish
ARG RUNTIME=linux-x64

RUN dotnet publish --configuration=Release --runtime=${RUNTIME} --self-contained \
     --framework=net5.0 --output /publish EventStore.ClusterNode

FROM mcr.microsoft.com/dotnet/runtime-deps:5.0-${CONTAINER_RUNTIME} AS runtime
ARG RUNTIME=linux-x64
ARG UID=1000
ARG GID=1000

RUN if [[ "${RUNTIME}" = "alpine-x64" ]];\
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

RUN printf "ExtIp: 0.0.0.0\n\
IntIp: 0.0.0.0" >> /etc/eventstore/eventstore.conf

VOLUME /var/lib/eventstore /var/log/eventstore

EXPOSE 1112/tcp 1113/tcp 2112/tcp 2113/tcp

HEALTHCHECK --interval=5s --timeout=5s --retries=24 \
    CMD curl --fail --insecure https://localhost:2113/health/live || curl --fail http://localhost:2113/health/live || exit 1

ENTRYPOINT ["/opt/eventstore/EventStore.ClusterNode"]
