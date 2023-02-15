import http from "k6/http";

import * as config from "../config.js";

import * as apiHelpers from "../apiHelpers.js";

export function postCloudEvent(serializedCloudEvent, token) {
  var endpoint = config.platformEvents.events;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/cloudevents+json"
  );

  var response = http.post(endpoint, serializedCloudEvent, params);

  return response;
}

export function getCloudEvents(token) {
  var endpoint = config.platformEvents.events;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/cloudevents+json"
  );

  var response = http.get(endpoint, params);

  return response;
}
