FROM mcr.microsoft.com/dotnet/sdk:9.0.203-alpine3.21@sha256:6fc61b57f8fa5f333e30c4192076fbd8b76f90dd2c5b2e5f9066f76f5b726832 AS build

COPY src/Events ./Events
COPY src/DbTools ./DbTools
COPY src/Events/Migration ./Migration

WORKDIR DbTools/
RUN dotnet build ./DbTools.csproj -c Release -o /app_tools

WORKDIR ../Events/

RUN dotnet build ./Altinn.Platform.Events.csproj -c Release -o /app_output
RUN dotnet publish ./Altinn.Platform.Events.csproj -c Release -o /app_output

FROM mcr.microsoft.com/dotnet/aspnet:9.0.4-alpine3.21@sha256:3fce6771d84422e2396c77267865df61174a3e503c049f1fe242224c012fde65 AS final

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
