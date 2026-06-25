import http from "k6/http";
import encoding from "k6/encoding";

import * as config from "../config.js";
import { stopIterationOnFail } from "../errorhandler.js";
import * as apiHelpers from "../apiHelpers.js";
import { getFromSecretSource } from "../secret-reader.js";

const environment = (__ENV.altinn_env || "").toLowerCase(); // Fallback value for when k6 inspect is run in script validation (env var evaluation yields 'undefined' in this phase)

/*
Generate enterprise token for test environment
*/
export function generateEnterpriseToken(queryParams) {
    let endpoint =
        config.tokenGenerator.getEnterpriseToken +
        apiHelpers.buildQueryParametersForEndpoint(queryParams);

    return generateToken(endpoint);
}

async function generateToken(endpoint) {
    const tokenGeneratorUserName = await getFromSecretSource(
        "tokenGeneratorUserName"
    );
    const tokenGeneratorUserPwd = await getFromSecretSource(
        "tokenGeneratorUserPwd"
    );

    const credentials = `${tokenGeneratorUserName}:${tokenGeneratorUserPwd}`;
    const encodedCredentials = encoding.b64encode(credentials);

    let params = apiHelpers.buildHeaderWithBasic(encodedCredentials);

    let response = http.get(endpoint, params);

    if (response.status != 200) {
        stopIterationOnFail("Token generation failed", false, response);
    }

    let token = response.body;
    return token;
}
