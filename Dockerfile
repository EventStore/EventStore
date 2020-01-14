ARG CONTAINER_RUNTIME=bionic
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-bionic AS build
ARG RUNTIME=linux-x64

WORKDIR /build/ci

COPY ./ci ./

WORKDIR /build/src

COPY ./src/EventStore.sln ./src/*/*.csproj ./src/Directory.Build.* ./

RUN for file in $(ls *.csproj); do mkdir -p ./${file%.*}/ && mv $file ./${file%.*}/; done

RUN dotnet restore --runtime=${RUNTIME}

COPY ./src .

RUN dotnet build --configuration=Release --runtime=${RUNTIME} --no-restore --framework=netcoreapp3.1

FROM build as test
ARG RUNTIME=linux-x64
RUN echo '#!/usr/bin/env sh\n\
find /build/src -maxdepth 1 -type d -name "*.Tests" -print0 | xargs -0 -n1 dotnet test --no-restore --blame --configuration=Release --results-directory=../testresults --verbosity=normal --logger=trx --runtime=${RUNTIME} --settings ../ci/ci.runsettings' \
    >> /build/test.sh && \
    chmod +x /build/test.sh
CMD ["/build/test.sh"]

FROM build as publish
ARG RUNTIME=linux-x64

RUN dotnet publish --configuration=Release --no-build --runtime=${RUNTIME} --self-contained \
     --framework=netcoreapp3.1 --output /publish /p:PublishTrimmed=true EventStore.ClusterNode

FROM mcr.microsoft.com/dotnet/core/runtime-deps:3.1-${CONTAINER_RUNTIME} AS runtime
ARG UID=1000
ARG GID=1000

RUN apt update && \
    apt install -y \
    curl && \
    rm -rf /var/lib/apt/lists/*

WORKDIR /opt/eventstore

RUN addgroup --gid ${GID} "eventstore" && \
    adduser \
    --disabled-password \
    --gecos "" \
    --ingroup "eventstore" \
    --no-create-home \
    --uid ${UID} \
    "eventstore"

COPY --from=publish /publish ./

RUN mkdir -p /var/lib/eventstore && \
    chown -R eventstore:eventstore /opt/eventstore /var/lib/eventstore

USER eventstore

VOLUME /var/lib/eventstore

EXPOSE 1112/tcp
EXPOSE 1113/tcp
EXPOSE 2112/tcp
EXPOSE 2113/tcp

HEALTHCHECK --interval=5s --timeout=5s --retries=24 \
    CMD curl --fail --insecure https://localhost:2113/health/live || exit 1

ENTRYPOINT ["/opt/eventstore/EventStore.ClusterNode"]
CMD ["--ext-ip", "0.0.0.0", "--int-ip", "0.0.0.0"]
