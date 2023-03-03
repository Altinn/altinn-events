# Altinn Events

Create and subscribe to events from apps or other sources.  
Documentation: https://docs.altinn.studio/api/events/  

## Build status
[![Events build status](https://dev.azure.com/brreg/altinn-studio/_apis/build/status/altinn-platform/events-master?label=altinn/events)](https://dev.azure.com/brreg/altinn-studio/_build/latest?definitionId=136)


## Getting Started

These instructions will get you a copy of the events component up and running on your machine for development and testing purposes.

### Prerequisites

1. [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
2. Code editor of your choice
3. Newest [Git](https://git-scm.com/downloads)
4. [Docker CE](https://www.docker.com/get-docker)
5. Solution is cloned
6. PostgreSQL is installed locally (see [handbook](https://docs.altinn.studio/community/contributing/handbook/postgres/))


## Running the events component

### In a docker container

Clone [Altinn Events repo](https://github.com/Altinn/altinn-events) and navigate to the root folder.

```cmd
docker-compose up -d --build
```

### With .NET

The Events components can be run locally when developing/debugging. Follow the install steps above if this has not already been done.

Stop the container running Events

```cmd
docker stop altinn-events
```

Navigate to src/Events, and build and run the code from there, or run the solution using you selected code editor

```cmd
cd src/Events
dotnet run
```

The events solution is now available locally at http://localhost:5080/api/v1
