FROM mcr.microsoft.com/dotnet/sdk:10.0.202-alpine3.23@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build

COPY src/Events ./Events
COPY src/DbTools ./DbTools
COPY src/Events.Common ./Events.Common

WORKDIR /DbTools
RUN dotnet build ./DbTools.csproj -c Release -o /app_tools

# Build the Events project
WORKDIR /Events
RUN dotnet build ./Altinn.Platform.Events.csproj -c Release -o /app_output
RUN dotnet publish ./Altinn.Platform.Events.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/sdk:10.0.202-alpine3.23@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build

EXPOSE 5080
WORKDIR /app
COPY --from=build /app_output .

COPY --from=build /Events/Migration ./Migration

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT ["dotnet", "Altinn.Platform.Events.dll"]
