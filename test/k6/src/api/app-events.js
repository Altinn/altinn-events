import http from "k6/http";
import * as apiHelper from "../apiHelpers.js";
import * as config from "../config.js";

export function getEventsForOrg(org, app, queryParams, token) {
  let endpoint = config.platformEvents.app + org + "/" + app;

  return getAppEvents(endpoint, queryParams, token);
}

export function getEventsForParty(queryParams, token) {
  let endpoint = config.platformEvents.app + "party";

  return getAppEvents(endpoint, queryParams, token);
}

export function getEventsFromNextLink(nextLink, token){
return getAppEvents(nextLink, null, token);
}

function getAppEvents(endpoint, queryParams, token) {
  endpoint +=
  queryParams == null
    ? "" : apiHelper.buildQueryParametersForEndpoint(queryParams);

  let params = apiHelper.buildHeaderWithBearer(token);

  let response = http.get(endpoint, params);

  return response;
}
