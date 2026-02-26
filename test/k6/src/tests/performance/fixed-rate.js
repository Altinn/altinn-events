/*
    Performance test: fixed arrival rate

    Fires exactly `targetRps` event POST requests per second for `duration`,
    regardless of how long each request takes. This decouples load generation
    from response time, which is the correct model for finding the system's
    throughput ceiling.

    If k6 cannot start iterations fast enough — because the system is overloaded,
    VUs are exhausted, or registration is too slow — `dropped_iterations` climbs
    above 0, telling you the target rate was not sustained.

    How to find the ceiling:
      Start low (e.g. -e targetRps=10) and increase until you see either
      dropped_iterations > 0 or a sharp spike in p(95) POST latency.
      That inflection point is your system's throughput ceiling.

    After the test, handleSummary() prints SQL queries with timestamps filled in:
      - SQL 1 ingest vs outbound counts    (registered, queued, webhook responses)
      - SQL 2 pipeline latency             (registration → OutboundQueue lag)
      - SQL 3 throughput per 5s bucket     (registration, queuing, webhook delivery)

    Command:
    podman compose run k6 run /src/tests/performance/fixed-rate.js \
      -e altinn_env=yt01 \
      -e tokenGeneratorUserName=autotest \
      -e tokenGeneratorUserPwd=*** \
      -e runId=$(date +%Y%m%d-%H%M%S) \
      -e targetRps=20 \
      -e duration=2m \
      -e maxVus=50 \
      -e queryOffsetMinutes=15

    Tuning preAllocatedVUs:
      k6 pre-warms this many VUs before the test starts. The rule of thumb is
      preAllocatedVUs ≈ targetRps × expected_p95_latency_in_seconds.
      This script defaults to targetRps × 2 (assumes up to 2s per request),
      capped by maxVus. Override maxVus if you need more headroom.
*/

import {
    runId, performanceSetup, postCloudEvent, performanceTeardown,
    warnIfUntagged, getTimeWindow, getBaseMetrics, buildSummary,
} from "./helpers.js";

const eventType = `performancetest.fixed-rate.${runId}`;
const targetRps = Number.parseInt(__ENV.targetRps || "20", 10);
const duration  = __ENV.duration || "2m";
const maxVus    = Number.parseInt(__ENV.maxVus || "50", 10);

// Pre-allocate enough VUs to sustain targetRps assuming up to ~2s per request.
// k6 will spin up more (up to maxVus) if needed mid-test.
const preAllocatedVUs = Math.min(targetRps * 2, maxVus);

warnIfUntagged();

export const options = {
    thresholds: {
        errors:         ["count<1"],
        http_req_duration:  ["p(95)<5000"],
        dropped_iterations: ["count<1"],
    },
    scenarios: {
        fixed_rate: {
            executor:        "constant-arrival-rate",
            rate:            targetRps,
            timeUnit:        "1s",
            duration:        duration,
            preAllocatedVUs: preAllocatedVUs,
            maxVUs:          maxVus,
        },
    },
};

export function setup() {
    return performanceSetup(`Target rate: ${targetRps} events/s for ${duration} (preAllocated: ${preAllocatedVUs}, max: ${maxVus} VUs)`);
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
    const droppedCount  = data.metrics["dropped_iterations"]?.values?.count ?? 0;
    const effectiveRps  = durationSec > 0 ? (successCount / durationSec).toFixed(1) : "N/A";
    const targetMet     = droppedCount === 0 ? "YES" : `NO — ${droppedCount} iterations dropped`;

    return buildSummary("Fixed-Rate Throughput Test — Summary                 ║", [
        "  k6 results:",
        `    Target rate               : ${targetRps} events/s`,
        `    Effective rate            : ${effectiveRps} events/s`,
        `    Target rate sustained     : ${targetMet}`,
        `    Events posted (HTTP 200)  : ${successCount}`,
        `    Errors                    : ${errorCount}`,
        `    Dropped iterations        : ${droppedCount}`,
        `    p(95) POST latency        : ${p95Ms.toFixed(0)} ms`,
    ], eventType, data);
}
