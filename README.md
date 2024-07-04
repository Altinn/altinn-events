# Altinn Events

Create and subscribe to events from apps or other sources.
Documentation: https://docs.altinn.studio/events

## Build status
[![Events build status](https://dev.azure.com/brreg/altinn-studio/_apis/build/status/altinn-platform/events-master?label=altinn/events)](https://dev.azure.com/brreg/altinn-studio/_build/latest?definitionId=136)


## Getting Started

These instructions will get you a copy of the events component up and running on your machine for development and testing purposes.

### Prerequisites

1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Newest [Git](https://git-scm.com/downloads)
3. A code editor - we like [Visual Studio Code](https://code.visualstudio.com/download)
   - Install [Azure Functions extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurefunctions). You can also install the [Azure Tools extension pack](https://marketplace.visualstudio.com/items?itemName=ms-vscode.vscode-node-azure-pack), which is recommended for working with Azure resources.
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop
5. [PostgreSQL](https://www.postgresql.org/download/)
6. [pgAdmin](https://www.pgadmin.org/download/)
7. Install [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#install-azurite)

### Setting up PostgreSQL

Ensure that both PostgreSQL and pgAdmin have been installed and start pgAdmin.

In pgAdmin
- Create database _eventsdb_
- Create the following users with password: _Password_ (see privileges in parentheses)
  - platform_events_admin (superuser, canlogin)
  - platform_events (canlogin)
- Create schema _events_ in eventsdb with owner _platform_events_admin_

A more detailed description of the database setup is available in [our developer handbook](https://docs.altinn.studio/community/contributing/handbook/postgres/)

### Cloning the application

Clone [Altinn Events repo](https://github.com/Altinn/altinn-events) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-events
cd altinn-events
```

### Running the application in a docker container

- [Start Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#run-azurite)

- Start Altinn Events docker container run the command

  ```cmd
  podman compose up -d --build
  ```

- To stop the container running Altinn Events run the command

  ```cmd
  podman stop altinn-events
  ```

The events solution is now available locally at http://localhost:5080/.
To access swagger use http://localhost:5080/swagger.

### Running the application with .NET

The Events components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

- [Start Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#run-azurite)

- Navigate to _src/Events_, and build and run the code from there, or run the solution using you selected code editor

  ```cmd
  cd src/Events
  dotnet run
  ```

The events solution is now available locally at http://localhost:5080/.
To access swagger use http://localhost:5080/swagger.

### Running functions

- [Start Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=visual-studio%2Cblob-storage#run-azurite)
  
Start Altinn Events Functions
```bash
cd src/Altinn.Events.Functions
func start
```