import http from "k6/http";
import { stopIterationOnFail } from "../errorhandler.js";

let baseUrl = "https://webhook.site/";
export function deleteAllRequests(webhookEndpointToken) {
  var endpoint = "https://webhook.site/token/" + webhookEndpointToken;
  http.del(endpoint);
}

export function createNewToken() {
  var endpoint = baseUrl + "token";

  var res = http.post(endpoint);

  if (res.status != 200) {
    stopIterationOnFail("Generating new test site token failed", false, res);
  }

  console.log(res.body);
  var responseObject = JSON.parse(res.body);

  return responseObject.uuid;
}
