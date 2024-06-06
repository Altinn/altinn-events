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
   - Also install [recommended extensions](https://code.visualstudio.com/docs/editor/extension-marketplace#_workspace-recommended-extensions) (e.g. [C#](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp))
4. [Podman](https://podman.io/) or another container tool such as Docker Desktop
5. PostgreSQL is installed locally (see [handbook](https://docs.altinn.studio/community/contributing/handbook/postgres/))


## Running the events component

Clone [Altinn Events repo](https://github.com/Altinn/altinn-events) and navigate to the folder.

```bash
git clone https://github.com/Altinn/altinn-events
cd altinn-events
```

### In a docker container


To start an Altinn Events docker container

```cmd
podman compose up -d --build
```

To stop the container running Altinn Events

```cmd
podman stop altinn-register
```

### With .NET

The Events components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

Navigate to _src/Events_, and build and run the code from there, or run the solution using you selected code editor

```cmd
cd src/Events
dotnet run
```

The events solution is now available locally at http://localhost:5080/