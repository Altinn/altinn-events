import http from "k6/http";

import * as config from "../config.js";

import * as apiHelpers from "../apiHelpers.js";

export function postCloudEvent(serializedCloudEvent, token) {
  var endpoint = config.platformEvents.events;

  var params = apiHelpers.buildHeaderWithBearerContentType(
    token,
    "application/cloudevents+json"
  );

  var response = http.post(endpoint, serializedCloudEvent, params);

  return response;
}

export function getCloudEvents(queryParams, token) {
  var endpoint = config.platformEvents.events;
  return getEvents(endpoint, queryParams, token);
}

export function getEventsFromNextLink(nextLink, token) {
  return getEvents(nextLink, null, token);
}

function getEvents(endpoint, queryParams, token) {
  endpoint +=
   queryParams != null
    ? apiHelpers.buildQueryParametersForEndpoint(queryParams)
    : "";

  var params = apiHelpers.buildHeaderWithBearerContentType(
    token,
    "application/cloudevents+json"
  );

  var response = http.get(endpoint, params);

  return response;
}

