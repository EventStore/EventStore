FROM mcr.microsoft.com/dotnet/core/sdk:3.0 AS build
WORKDIR /es

COPY ./ .
RUN dotnet restore ./src/EventStore.sln

WORKDIR /es/src
RUN dotnet publish -c Debug -o ../../out

WORKDIR /out
RUN ls -la

FROM mcr.microsoft.com/dotnet/core/aspnet:3.0 AS dest
WORKDIR /eventstore
COPY --from=build /out/ ./

EXPOSE 1112/tcp
EXPOSE 1113/tcp
EXPOSE 2112/tcp
EXPOSE 2113/tcp

ENTRYPOINT ["dotnet", "EventStore.ClusterNode.dll"]

