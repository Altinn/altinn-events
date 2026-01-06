import http from "k6/http";

import * as config from "../config.js";

import * as apiHelpers from "../apiHelpers.js";

export function postCloudEvent(serializedCloudEvent, token) {
  let endpoint = config.platformEvents.events;

  let params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/cloudevents+json"
  );

  let response = http.post(endpoint, serializedCloudEvent, params);

  return response;
}

export function getCloudEvents(queryParams, token) {
  let endpoint = config.platformEvents.events;
  return getEvents(endpoint, queryParams, token);
}

export function getEventsFromNextLink(nextLink, token) {
  return getEvents(nextLink, null, token);
}

function getEvents(endpoint, queryParams, token) {
  endpoint +=
   queryParams == null
    ? "" : apiHelpers.buildQueryParametersForEndpoint(queryParams);

  let params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/cloudevents+json"
  );

  let response = http.get(endpoint, params);

  return response;
}

