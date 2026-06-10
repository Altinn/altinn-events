## k6 test project for automated tests

# Getting started


Install pre-requisites
## Install k6

*We recommend running the tests through a docker container.*

From the command line:

> docker pull grafana/k6


Further information on [installing k6 for running in docker is available here.](https://k6.io/docs/get-started/installation/#docker)


Alternatively, it is possible to run the tests directly on your machine as well.

[General installation instructions are available here.](https://k6.io/docs/get-started/installation/)

## Configuring the secret source

**Never put secrets on the command line** - sensitive values should be passed to the k6 script via a secret source, as this provides full k6 redaction. In other words, secrets loaded via k6/secrets are automatically redacted from all k6 log output as `***SECRET_REDACTED***`, effectively preventing the values to leak into logs.

1. Create a `.secrets` file in the k6 folder
2. Copy contents from `.secrets.sample`
3. Assign valid values to the variables

## Running tests

All tests are defined in `src/tests` and in the top of each test file an example of the cmd to run the test is available.

The command should be run from the root of the k6 folder.

>$> cd /altinn-events/test/k6

Run test suite by specifying filename.

For example:

>$> podman compose run k6 run /src/tests/events/post.js --secret-source=file=/.secrets -e env=***

The command consists of three sections

`podman compose run` to run the test in a docker container

`k6 run {path to test file}` pointing to the test file you want to run e.g. `/src/tests/events/post.js`


`--secret-source=file=/.secrets -e env=***` all environment variables that should be included in the request.


### Webhook for subscriptions

When testing the subscriptions a webhook must be provided.
You are free to provide whichever endpoint, but make sure it ends with `/`.

We would suggest to use webhook.site.
The following PowerShell script will generate a dedicated webhook to provide as the environment variable `webhookEndpoint`


```ps
$params = @{
 Uri = "https://webhook.site/token"
 Method = "Post"
}

$webhookToken =((Invoke-Webrequest @params).Content  | ConvertFrom-Json).uuid

$webhookEndpoint= "https://webhook.site/" + $webhookToken + "/"
```
