// Baseurls for platform
export var baseUrls = {
  at21: "at21.altinn.cloud",
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  tt02: "tt02.altinn.no",
  prod: "altinn.no",
};

var maskinportenBaseUrls = {
  tt02: "https://test.maskinporten.no/",
  prod: "https://maskinporten.no/",
};

//Get values from environment
const environment = __ENV.env.toLowerCase();
export let baseUrl = baseUrls[environment];
let maskinportenBaseUrl = maskinportenBaseUrls[environment];

//AltinnTestTools
export var tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
  getPersonalToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken",
};

// Platform Events
export var platformEvents = {
  events:
    "https://platform." + baseUrl + "/events/api/v1/events/",
  app:
    "https://platform." + baseUrl + "/events/api/v1/app/",
  subscriptions:
    "https://platform." + baseUrl + "/events/api/v1/subscriptions/",
};

export var platformAuthentication = {
  exchange:
    "https://platform." + baseUrl + "/authentication/api/v1/exchange/maskinporten",
  refresh:
  "https://platform." + baseUrl + "/authentication/api/v1/authentication?goto=" +
  "https://platform." + baseUrl + "/authentication/api/v1/refresh",
};

export var portalAuthentication = {
  authenticateWithPwd:
    "https://" + baseUrl + "/api/authentication/authenticatewithpassword",
};

// Maskinporten
export var maskinporten = {
  audience: maskinportenBaseUrl,
  token: maskinportenBaseUrl + "token",
};
