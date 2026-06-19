import * as config from "../config.js";
import {
    buildHeaderWithRuntimeForMultipart,
    MULTIPART_BOUNDARY,
} from "../apiHelpers.js";
import http from "k6/http";

/**
 * Api call to App Api:Instances to create an app instance for a party with multipart data including form data xml and returns response
 * @param {*} altinnStudioRuntimeCookie token to authenticate the api request
 * @param {*} partyId partyid of the user to create an instance
 * @param {*} appOwner name of the app owner
 * @param {*} appName name of the app
 * @param {XMLDocument} formDataXml xml form data
 */
export function postInstanceWithMultipartData(
    altinnStudioRuntimeCookie,
    partyId,
    appOwner,
    appName,
    formDataXml
) {
    const endpoint = config.appApiBaseUrl(appOwner, appName) + "/instances";
    const params = buildHeaderWithRuntimeForMultipart(
        altinnStudioRuntimeCookie
    );

    let instanceJson = {
        instanceOwner: {
            partyId: partyId,
        },
    };
    instanceJson = JSON.stringify(instanceJson);

    const requestBody =
        `--${MULTIPART_BOUNDARY}\r\n` +
        `Content-Type: application/json; charset=utf-8\r\n` +
        `Content-Disposition: form-data; name=\"instance\"\r\n\r\n${instanceJson}\r\n\r\n` +
        `--${MULTIPART_BOUNDARY}\r\n` +
        `Content-Type: application/xml\r\n` +
        `Content-Disposition: form-data; name=\"default\"\r\n\r\n${formDataXml}\r\n\r\n` +
        `--${MULTIPART_BOUNDARY}--`;

    return http.post(endpoint, requestBody, params);
}
