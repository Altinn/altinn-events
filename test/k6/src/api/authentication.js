import { check } from "k6";
import http from "k6/http";

import { buildHeaderWithBearer } from "../apiHelpers.js";
import { platformAuthentication } from "../config.js";
import { stopIterationOnFail, addErrorCount } from "../errorhandler.js";

const userName = __ENV.userName;
const userPassword = __ENV.userPassword;

export function exchangeToAltinnToken(token, test) {
    const endpoint = platformAuthentication.exchange + "?test=" + test;
    const params = buildHeaderWithBearer(token);

    const res = http.get(endpoint, params);
    const success = check(res, {
        "// Setup // Authentication towards Altinn 3 Success": (r) =>
            r.status === 200,
    });
    addErrorCount(success);
    stopIterationOnFail(
        "// Setup // Authentication towards Altinn 3  Failed",
        success,
        res
    );

    return res.body;
}
