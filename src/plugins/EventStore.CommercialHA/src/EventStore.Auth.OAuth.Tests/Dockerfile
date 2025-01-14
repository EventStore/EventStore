ARG ES_IMAGE=docker.eventstore.com/eventstore-staging-ce/eventstoredb-ce:ci

FROM mcr.microsoft.com/dotnet/core/sdk:8.0-jammy AS build

WORKDIR /build/src

COPY ./src/EventStore.CommercialHA.sln ./src/*/*.csproj ./src/Directory.Build.props ./

RUN for file in $(ls *.csproj); do mkdir -p ./${file%.*}/ && mv $file ./${file%.*}/; done

RUN dotnet restore --runtime=${RUNTIME}

COPY ./src .

RUN dotnet build --configuration=Release --runtime=${RUNTIME} --no-restore --framework=net8.0 && \
	rm /build/bin/Release/EventStore.Auth.OAuth/net8.0/EventStore.Plugins.*

FROM ${ES_IMAGE} as eventstore

RUN mkdir -p /opt/eventstore/plugins

COPY --from=build /build/bin/Release/EventStore.Auth.OAuth/net8.0 /opt/eventstore/plugins/oauth

