FROM mcr.microsoft.com/dotnet/sdk:10.0.102-alpine3.23@sha256:48b443d87fe7426ce6a20554e87fdd6189143dc4aa5f43b501acf5fef6d42740 AS build

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

FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-alpine3.23@sha256:55fa85c0075d8b48b4854af502ffbb305470564e9125c227da28b52cfec6dc92 AS final

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
