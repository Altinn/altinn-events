## Folder for collecting all k6 tests related to events

### Prerequisites

We reccomend running the tests through a docker container. 
[Instructions on installing k6 for running in docker is available here.](https://k6.io/docs/get-started/installation/#docker)


It is possible to run the tests directly on your machine as well. 
[General installation instructions are available here.](https://k6.io/docs/get-started/installation/)

### Running tests

All tests are defined in `src/tests` and in the top of each test file an example of the cmd to run the test is available.

The command should be run from the root of the k6 folder.

The comand consists of three sections

`docker-compose run` to run the test in a docker container

`k6 run {path to test file}` pointing to the test file you want to run e.g. `src/test/events.js`


`-e tokenGeneratorUserName=*** -e tokenGeneratorUserPwd=*** -e env=***` all environment variables that should be included in the request.
