FROM mcr.microsoft.com/dotnet/sdk:9.0.306-alpine3.22@sha256:f271ed7d0fd9c5a7ed0acafed8a2bc978bb65c19dcd2eeea0415adef142ffc87 AS build

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

FROM mcr.microsoft.com/dotnet/aspnet:9.0.10-alpine3.22@sha256:5e8dca92553951e42caed00f2568771b0620679f419a28b1335da366477d7f98 AS final

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
