import http from "k6/http";
import { stopIterationOnFail } from "../errorhandler.js";

import * as config from "../config.js";

export function postCloudEvent(serializedCloudEvent, token) {
  var endpoint = config.platformEvents.events;

  var params = {
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/cloudevents+json",
    },
  };

  var response = http.post(endpoint, serializedCloudEvent, params);

  if (response.status != 200) {
    stopIterationOnFail("POST to events/api/v1/events failed", false, response);
  }

  return response.status;
}
