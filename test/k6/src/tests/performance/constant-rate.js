/*
    Performance test: constant arrival rate

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
      That inflection point is your system's throughput ceiling for that infra.

    Comparing ASQ vs ASB:
      Run with the same targetRps on each infra. Look at SQL 2 (pipeline
      latency) and the per-bucket counts in SQL 3 to see which processes
      events more quickly at identical ingest rates.

    Command:
    podman compose run k6 run /src/tests/performance/constant-rate.js \
      -e altinn_env=yt01 \
      -e tokenGeneratorUserName=autotest \
      -e tokenGeneratorUserPwd=*** \
      -e runId=$(date +%Y%m%d-%H%M%S) \
      -e targetRps=20 \
      -e duration=2m \
      -e maxVus=50

    queryOffsetMinutes controls how far past the test end the SQL queries look (default: 15).
    -e queryOffsetMinutes=30

    Tuning preAllocatedVUs:
      k6 pre-warms this many VUs before the test starts. The rule of thumb is
      preAllocatedVUs ≈ targetRps × expected_p95_latency_in_seconds.
      This script defaults to targetRps × 2 (assumes up to 2s per request),
      capped by maxVus. Override maxVus if you need more headroom.
*/

import { check } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import * as subscriptionsApi from "../../api/subscriptions.js";
import * as config from "../../config.js";
import { addErrorCount } from "../../errorhandler.js";

const scopes = "altinn:events.publish altinn:serviceowner altinn:events.subscribe";
const resourceFilter = "urn:altinn:resource:ttd-altinn-events-automated-tests";

const runId     = __ENV.runId || "untagged";
const eventType = `performancetest.constant-rate.${runId}`;
const targetRps = Number.parseInt(__ENV.targetRps || "20", 10);
const duration  = __ENV.duration || "2m";
const maxVus    = Number.parseInt(__ENV.maxVus || "50", 10);

// Pre-allocate enough VUs to sustain targetRps assuming up to ~2s per request.
// k6 will spin up more (up to maxVus) if needed mid-test.
const preAllocatedVUs = Math.min(targetRps * 2, maxVus);

if (runId === "untagged") {
    console.warn("[WARN] No runId provided. Pass -e runId=$(date +%Y%m%d-%H%M%S) to isolate this run.");
}

export const options = {
    thresholds: {
        error_rate:         ["count<1"],
        http_req_duration:  ["p(95)<5000"],
        dropped_iterations: ["count<1"],
    },
    scenarios: {
        constant_rate: {
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
    const token = setupToken.getAltinnTokenForOrg(scopes);

    const subscription = {
        endPoint: config.platformEvents.webhookReceiver,
        resourceFilter: resourceFilter,
    };

    const response = subscriptionsApi.postSubscription(JSON.stringify(subscription), token);

    const success = check(response, {
        "Setup: subscription created. Status is 201": (r) => r.status === 201,
    });

    if (!success) {
        throw new Error(`[SETUP] Failed to create subscription: HTTP ${response.status} – ${response.body}`);
    }

    const subscriptionId = JSON.parse(response.body).id;
    console.log(`[SETUP] Subscription ${subscriptionId} created`);
    console.log(`[SETUP] Target rate: ${targetRps} events/s for ${duration} (preAllocated: ${preAllocatedVUs}, max: ${maxVus} VUs)`);

    return { token, subscriptionId };
}

export default function runTests(data) {
    const cloudEvent = {
        id: uuidv4(),
        source: "https://github.com/Altinn/altinn-events/tree/main/test/k6",
        specversion: "1.0",
        type: eventType,
        subject: "/autotest/k6",
        resource: resourceFilter,
        time: new Date().toISOString(),
    };

    const response = eventsApi.postCloudEvent(JSON.stringify(cloudEvent), data.token);

    const success = check(response, {
        "POST event. Status is 200": (r) => r.status === 200,
    });

    addErrorCount(success);
}

// Intentionally does NOT delete the subscription — webhook delivery and
// WebhookPostResponse trace_log entries happen asynchronously after the test ends.
export function teardown(data) {
    if (data.subscriptionId) {
        console.log(`[TEARDOWN] Subscription ${data.subscriptionId} left active — delete manually when done.`);
    }
}

export function handleSummary(data) {
    const testEndTime    = new Date();
    const testDurationMs = data.state.testRunDurationMs;
    const testStartTime  = new Date(testEndTime.getTime() - testDurationMs);

    const offsetMinutes = Number.parseInt(__ENV.queryOffsetMinutes || "15", 10);
    const queryEndTime  = new Date(testEndTime.getTime() + offsetMinutes * 60 * 1000);

    const eventsPosted   = data.metrics["http_reqs"]?.values?.count ?? 0;
    const errorCount     = data.metrics["error_rate"]?.values?.count ?? 0;
    const droppedCount   = data.metrics["dropped_iterations"]?.values?.count ?? 0;
    const p95Ms          = data.metrics["http_req_duration"]?.values?.["p(95)"] ?? 0;
    const successCount   = eventsPosted - errorCount;
    const durationSec    = testDurationMs / 1000;
    const effectiveRps   = durationSec > 0 ? (successCount / durationSec).toFixed(1) : "N/A";
    const targetMet      = droppedCount === 0 ? "YES" : `NO — ${droppedCount} iterations dropped`;

    const sPg = testStartTime.toISOString().replace("T", " ").replace("Z", "+00");
    const ePg = queryEndTime.toISOString().replace("T", " ").replace("Z", "+00");

    const lines = [
        "",
        "╔══════════════════════════════════════════════════════════════╗",
        "║         Constant-Rate Throughput Test — Summary              ║",
        "╚══════════════════════════════════════════════════════════════╝",
        "",
        "  k6 results:",
        `    Target rate               : ${targetRps} events/s`,
        `    Effective rate            : ${effectiveRps} events/s`,
        `    Target rate sustained     : ${targetMet}`,
        `    Events posted (HTTP 200)  : ${successCount}`,
        `    Errors                    : ${errorCount}`,
        `    Dropped iterations        : ${droppedCount}`,
        `    p(95) POST latency        : ${p95Ms.toFixed(0)} ms`,
        "",
        `  Run ID    : ${runId}${runId === "untagged" ? "  ⚠ pass -e runId=... to isolate this run" : ""}`,
        `  Event type: ${eventType}`,
        `  Query window: ${testStartTime.toISOString()} → ${queryEndTime.toISOString()}  (+${offsetMinutes} min offset)`,
        "  Override offset with -e queryOffsetMinutes=N",
        "",

        "  ════════════════════════════════════════════════════════════",
        "  SQL — events.trace_log",
        "  ════════════════════════════════════════════════════════════",
        "",
        "  -- SQL 1: ingest vs outbound counts ────────────────────────",
        "  SELECT",
        "      COUNT(*) FILTER (WHERE activity = 'Registered')          AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue')       AS events_queued,",
        "      COUNT(*) FILTER (WHERE activity = 'WebhookPostResponse') AS events_webhook_response,",
        "      COUNT(*) FILTER (WHERE activity = 'Unauthorized')        AS events_unauthorized",
        "  FROM events.trace_log",
        `  WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        `    AND eventtype = '${eventType}';`,
        "",
        "  -- SQL 2: pipeline latency per event (Registered → OutboundQueue) ──",
        "  SELECT",
        "      percentile_cont(0.50) WITHIN GROUP (ORDER BY lag_ms) AS p50_ms,",
        "      percentile_cont(0.95) WITHIN GROUP (ORDER BY lag_ms) AS p95_ms,",
        "      percentile_cont(0.99) WITHIN GROUP (ORDER BY lag_ms) AS p99_ms,",
        "      MAX(lag_ms)                                           AS max_ms",
        "  FROM (",
        "      SELECT",
        "          cloudeventid,",
        "          EXTRACT(EPOCH FROM (",
        "              MAX(time) FILTER (WHERE activity = 'OutboundQueue')",
        "            - MIN(time) FILTER (WHERE activity = 'Registered')",
        "          )) * 1000 AS lag_ms",
        "      FROM events.trace_log",
        `      WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        `        AND eventtype = '${eventType}'`,
        "        AND activity IN ('Registered', 'OutboundQueue')",
        "      GROUP BY cloudeventid",
        "      HAVING COUNT(DISTINCT activity) = 2",
        "  ) t;",
        "",
        "  -- SQL 3: actual rate per 5s bucket (compare to target) ────",
        "  SELECT",
        "      to_timestamp(floor(extract(epoch FROM time) / 5) * 5) AT TIME ZONE 'UTC' AS bucket,",
        "      COUNT(*) FILTER (WHERE activity = 'Registered')          AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue')       AS events_queued,",
        "      COUNT(*) FILTER (WHERE activity = 'WebhookPostResponse') AS events_webhook_response",
        "  FROM events.trace_log",
        `  WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        "    AND activity IN ('Registered', 'OutboundQueue', 'WebhookPostResponse')",
        `    AND eventtype = '${eventType}'`,
        "  GROUP BY bucket ORDER BY bucket;",
        "",
        `  Test ended at: ${testEndTime.toISOString()}`,
        `  Query window ends at: ${queryEndTime.toISOString()}`,
        "",
        "  ⚠  Subscription was NOT deleted — webhooks are still delivering.",
        "     Check [SETUP] log above for the subscription ID, then delete",
        "     it manually once SQL 1 shows events_webhook_response ≈ events_queued.",
        "",

        "  ════════════════════════════════════════════════════════════",
        "  Cleanup SQL — remove all test data for this run",
        "  ════════════════════════════════════════════════════════════",
        "  -- Run AFTER verifying results. Wrap in a transaction so you",
        "  -- can ROLLBACK if the counts look wrong before committing.",
        "",
        "  BEGIN;",
        `  DELETE FROM events.trace_log WHERE eventtype = '${eventType}' AND time BETWEEN '${sPg}' AND '${ePg}';`,
        `  DELETE FROM events.events    WHERE type      = '${eventType}' AND time BETWEEN '${sPg}' AND '${ePg}';`,
        "  -- Verify counts are 0 before committing:",
        `  -- SELECT COUNT(*) FROM events.trace_log WHERE eventtype = '${eventType}' AND time BETWEEN '${sPg}' AND '${ePg}';`,
        `  -- SELECT COUNT(*) FROM events.events    WHERE type      = '${eventType}' AND time BETWEEN '${sPg}' AND '${ePg}';`,
        "  COMMIT; -- or ROLLBACK",
        "",
    ];

    return { stdout: lines.join("\n") + "\n" };
}
