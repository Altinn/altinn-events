import http from "k6/http";
import { stopIterationOnFail } from "../errorhandler.js";
import * as apiHelpers from "../apiHelpers.js";
import * as config from "../config.js";

export function hello(token){
  var endpoint = config.platformEvents.subscriptions;
  return getSubscriptions(endpoint, token);

}

export function getAllSubscriptions(token) {
  var endpoint = config.platformEvents.subscriptions;
  return getSubscriptions(endpoint, token);
}

export function getSubscriptionById(id, token) {
  var endpoint = config.platformEvents.subscriptions + "/" + id;
  return getSubscriptions(endpoint, token);
}

export function postSubscription(serializedSubscription, token) {
  var endpoint = config.platformEvents.subscriptions;

  var params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/json"
  );

  var response = http.post(endpoint, serializedSubscription, params);

  return response;
}

export function deleteSubscription(id, token) {
  var endpoint = config.platformEvents.subscriptions + id;
  var params = apiHelpers.buildHeaderWithBearer(token);
  var response = http.del(endpoint,null, params);
  return response;
}

function getSubscriptions(endpoint, token) {
  var params = apiHelpers.buildHeaderWithBearerAndContentType(
    token,
    "application/json"
  );

  var response = http.get(endpoint, params);

  return response;
}
