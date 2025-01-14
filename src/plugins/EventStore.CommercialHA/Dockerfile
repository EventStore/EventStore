ARG ES_IMAGE=docker.eventstore.com/eventstore-staging-ce/eventstoredb-ce:ci

#
# image for building the plugins
#
FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build

ARG RUNTIME=linux-x64

# create dir
WORKDIR /publish

WORKDIR /build
COPY ./LICENSE.md ./
COPY ./NOTICE.html ./

WORKDIR /build/src

COPY ./src/EventStore.CommercialHA.sln ./src/*/*.csproj ./src/Directory.Build.props ./

RUN for file in $(ls *.csproj); do mkdir -p ./${file%.*}/ && mv $file ./${file%.*}/; done

RUN dotnet restore --runtime=${RUNTIME}

COPY ./src .

RUN dotnet publish --configuration=Release --runtime=${RUNTIME}

WORKDIR /build

RUN find bin -name publish -type d | cut -d'/' -f3 | xargs -I{} cp -R bin/Release/{}/net8.0/linux-x64/publish /publish/{}

#
# eventstore image that the plugins are copied into
#
FROM ${ES_IMAGE} as eventstore

USER root

RUN mkdir -p /opt/eventstore/plugins && chown -R eventstore:eventstore /opt/eventstore/plugins

COPY --chown=eventstore:eventstore --from=build /publish /opt/eventstore/plugins

USER eventstore
