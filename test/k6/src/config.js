// Baseurls for platform
export var baseUrls = {
  at21: "at21.altinn.cloud",
  at22: "at22.altinn.cloud",
  at23: "at23.altinn.cloud",
  at24: "at24.altinn.cloud",
};

//Get values from environment
const environment = __ENV.env.toLowerCase();
export let baseUrl = baseUrls[environment];

//AltinnTestTools
export var tokenGenerator = {
  getEnterpriseToken:
    "https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken",
};

// Platform Events
export var platformEvents = {
  events: "https://platform." + baseUrl + "/events/api/v1/events/",
  app: "https://platform." + baseUrl + "/events/api/v1/app/",
};
