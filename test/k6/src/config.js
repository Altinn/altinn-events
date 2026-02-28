// Baseurls for platform
export const baseUrls = {
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
  yt01: "yt01.altinn.cloud",
  tt02: "tt02.altinn.no",
  prod: "altinn.no",
};

let maskinportenBaseUrls = {
  tt02: "https://test.maskinporten.no/",
  prod: "https://maskinporten.no/",
};

// Auth cookie names in the different environments. NB: Must be updated until changes
// are rolled out to all environments
export const authCookieNames = {
  at22: '.AspxAuthCloud',
  at23: '.AspxAuthCloud',
  at24: '.AspxAuthCloud',
  tt02: '.AspxAuthTT02',
  yt01: '.AspxAuthYt',
  prod: '.AspxAuthProd',
};

//Get values from environment
const environment = (__ENV.altinn_env || '').toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)
export const baseUrl = baseUrls[environment];
export const authCookieName = authCookieNames[environment];

let maskinportenBaseUrl = maskinportenBaseUrls[environment];

//AltinnTestTools
export const tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
  getPersonalToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetPersonalToken",
};

// Platform Events
export const platformEvents = {
  events:
    "https://platform." + baseUrl + "/events/api/v1/events/",
  app:
    "https://platform." + baseUrl + "/events/api/v1/app/",
  subscriptions:
    "https://platform." + baseUrl + "/events/api/v1/subscriptions/",
  webhookReceiver:
    "https://platform." + baseUrl + "/events/api/v1/tests/webhookreceiver",
};

export const platformAuthentication = {
  exchange:
    "https://platform." + baseUrl + "/authentication/api/v1/exchange/maskinporten",
  refresh:
    "https://platform." + baseUrl + "/authentication/api/v1/authentication?goto=" +
    "https://platform." + baseUrl + "/authentication/api/v1/refresh",
};

export const portalAuthentication = {
  authenticateWithPwd:
    "https://" + baseUrl + "/api/authentication/authenticatewithpassword",
};

// Maskinporten
export const maskinporten = {
  audience: maskinportenBaseUrl,
  token: maskinportenBaseUrl + "token",
};
