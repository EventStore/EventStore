FROM mcr.microsoft.com/dotnet/core/sdk:3.1-bionic AS build
WORKDIR /es

COPY ./ .
RUN dotnet restore ./src/EventStore.sln

WORKDIR /es/src
RUN dotnet publish -c Debug -o ../../out

WORKDIR /out
RUN ls -la

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-bionic AS dest
WORKDIR /eventstore
COPY --from=build /out/ ./

EXPOSE 1112/tcp
EXPOSE 1113/tcp
EXPOSE 1114/tcp
EXPOSE 2112/tcp
EXPOSE 2113/tcp
EXPOSE 2114/tcp

ENTRYPOINT ["dotnet", "EventStore.ClusterNode.dll"]
CMD ["--ext-ip", "0.0.0.0", "--int-ip", "0.0.0.0", "--certificate-file", "/eventstore/dev-cert.pfx"]
