import http from "k6/http";

export function deleteAllRequests(webhookEndpointToken){
    var endpoint = "https://webhook.site/token/" + webhookEndpointToken;
    http.del(endpoint);
}