FROM mcr.microsoft.com/dotnet/sdk:10.0.101-alpine3.22@sha256:0fba99926e4f12405c78f37941312f00c1aadb178bd63616f5d96fc2af5a26a9 AS build

COPY src/Events ./Events
COPY src/DbTools ./DbTools
COPY src/Events.Common ./Events.Common
COPY src/Events/Migration ./Migration

WORKDIR /DbTools
RUN dotnet build ./DbTools.csproj -c Release -o /app_tools

# Build the Events project
WORKDIR /Events
RUN dotnet build ./Altinn.Platform.Events.csproj -c Release -o /app_output
RUN dotnet publish ./Altinn.Platform.Events.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:10.0.1-alpine3.23@sha256:900e8fc49fe97a7a8d9134612438d33c5c0e943dae8081b060054093d16e6ab4 AS final

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
