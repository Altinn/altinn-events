/*
    Performance test: fixed total events (shared-iterations)

    Sends exactly `totalEvents` events as fast as possible, shared across `vus` VUs.
    The test ends when all iterations are consumed — not after a fixed duration.

    Key metrics to compare between runs:
      - Test duration         (how long to post all events)
      - Effective POST rate   (events/s calculated from duration)
      - p(95) POST latency    (registration speed)
      - SQL 1 counts          (how many made it through the pipeline)
      - SQL 2 pipeline latency (registration → OutboundQueue lag)
      - SQL 3 throughput per 5s bucket (registration, queuing, webhook delivery)

    Command:
    podman compose run k6 run /src/tests/performance/fixed-total.js \
      -e altinn_env=yt01 \
      -e tokenGeneratorUserName=autotest \
      -e tokenGeneratorUserPwd=*** \
      -e runId=$(date +%Y%m%d-%H%M%S) \
      -e totalEvents=500 \
      -e vus=10 \
      -e queryOffsetMinutes=15
*/

import {
    runId, performanceSetup, postCloudEvent, performanceTeardown,
    warnIfUntagged, getTimeWindow, getBaseMetrics, buildSummary,
} from "./helpers.js";

const eventType   = `performancetest.fixed-total.${runId}`;
const totalEvents = Number.parseInt(__ENV.totalEvents || "500", 10);
const vus         = Number.parseInt(__ENV.vus || "10", 10);

warnIfUntagged();

export const options = {
    thresholds: {
        errors: ["count<1"],
        http_req_duration: ["p(95)<5000"],
    },
    scenarios: {
        fixed_total: {
            executor: "shared-iterations",
            vus: vus,
            iterations: totalEvents,
            maxDuration: "30m", // safety cap — increase if posting is very slow
        },
    },
};

export function setup() {
    return performanceSetup(`Will post ${totalEvents} events across ${vus} VUs`);
}

export default function runTests(data) {
    postCloudEvent(eventType, data.token);
}

export function teardown(data) {
    performanceTeardown(data);
}

export function handleSummary(data) {
    const { durationSec } = getTimeWindow(data);
    const { successCount, errorCount, p95Ms } = getBaseMetrics(data);
    const effectiveRate = durationSec > 0 ? (successCount / durationSec).toFixed(1) : "N/A";

    return buildSummary("Fixed-Total Throughput Test — Summary                ║", [
        "  k6 results (what was sent):",
        `    Target events             : ${totalEvents}`,
        `    Events posted (HTTP 200)  : ${successCount}`,
        `    Errors                    : ${errorCount}`,
        `    Test duration             : ${durationSec.toFixed(1)} s`,
        `    Effective POST rate       : ${effectiveRate} events/s`,
        `    p(95) POST latency        : ${p95Ms.toFixed(0)} ms`,
    ], eventType, data);
}
