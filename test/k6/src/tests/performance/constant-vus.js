/*
    Performance test: end-to-end event throughput (registration → outbound queue)

    A single subscription is created once in setup(). The main loop then posts events
    as fast as possible for the configured duration. Every posted event that matches
    the subscription triggers the full outbound pipeline, ending with the event being
    enqueued on the outbound Service Bus queue (TraceLogActivity.OutboundQueue).

    After the test, handleSummary() prints k6 results and ready-to-run SQL queries
    against the trace_log table with the exact test timestamps already filled in.

    Command:
    podman compose run k6 run /src/tests/performance/constant-vus.js \
      -e altinn_env=yt01 \
      -e tokenGeneratorUserName=autotest \
      -e tokenGeneratorUserPwd=*** \
      -e runId=$(date +%Y%m%d-%H%M%S) \
      -e vus=10 \
      -e duration=5m

    queryOffsetMinutes controls how far past the test end the queries look.
    Increase if outbound queue processing is expected to lag (default: 15).
    -e queryOffsetMinutes=30

    Pass a runId to isolate this run's trace_log rows from concurrent traffic.
    __ENV variables are consistent across all VUs (unlike module-level code which
    runs once per VU). Without runId, all events share the type
    "performancetest.constant-vus.untagged" — fine for solo runs, ambiguous otherwise.
    -e runId=$(date +%Y%m%d-%H%M%S)
*/

import { check } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import * as subscriptionsApi from "../../api/subscriptions.js";
import * as config from "../../config.js";
import { addErrorCount } from "../../errorhandler.js";

const scopes = "altinn:events.publish altinn:serviceowner altinn:events.subscribe";

// Resource used for both the posted events and the subscription filter.
// Must match so the subscription picks up every event we post.
const resourceFilter = "urn:altinn:resource:ttd-altinn-events-automated-tests";

// Unique identifier for this test run, embedded in every event's type field so
// trace_log rows can be filtered to exactly this run.
//
// IMPORTANT: module-level code runs once per VU, so uuidv4() here would produce
// a different value for each VU. __ENV variables are evaluated once before any VU
// starts and are therefore consistent across all VUs — the correct place for this.
const runId    = __ENV.runId || "untagged";
const vus      = Number.parseInt(__ENV.vus || "10", 10);
const duration = __ENV.duration || "5m";
const eventType = `performancetest.constant-vus.${runId}`;

if (runId === "untagged") {
    console.warn("[WARN] No runId provided. Events will use type 'performancetest.constant-vus.untagged'.");
    console.warn("[WARN] Pass -e runId=$(date +%Y%m%d-%H%M%S) to isolate this run in trace_log queries.");
}

export const options = {
    thresholds: {
        error_rate: ["count<1"],
        http_req_duration: ["p(95)<5000"],
    },
    vus: vus,
    duration: duration,
};

// setup() runs once before any VU starts. Creates the subscription and returns
// shared data to all VUs.
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

    return { token, subscriptionId };
}

// Default function — called once per VU iteration for the full test duration.
// Posts a single cloud event that matches the subscription so the outbound
// queue pipeline is exercised on every iteration.
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

// teardown() runs once after all VUs have finished.
// Intentionally does NOT delete the subscription — webhook delivery and
// WebhookPostResponse trace_log entries happen asynchronously after the test ends.
// Deleting the subscription here would pull it out from under in-flight
// Service Bus deliveries. Delete manually once you've verified the DB results.
export function teardown(data) {
    if (data.subscriptionId) {
        console.log(`[TEARDOWN] Subscription ${data.subscriptionId} left active — delete manually when done.`);
    }
}

// handleSummary() runs after teardown.
// Prints k6 results and copy-paste SQL queries with actual timestamps.
export function handleSummary(data) {
    const testEndTime = new Date();
    const testDurationMs = data.state.testRunDurationMs;
    const testStartTime = new Date(testEndTime.getTime() - testDurationMs);

    const offsetMinutes = Number.parseInt(__ENV.queryOffsetMinutes || "15", 10);
    const queryEndTime = new Date(testEndTime.getTime() + offsetMinutes * 60 * 1000);

    const eventsPosted = data.metrics["http_reqs"]?.values?.count ?? 0;
    const errorCount   = data.metrics["error_rate"]?.values?.count ?? 0;
    const p95Ms        = data.metrics["http_req_duration"]?.values?.["p(95)"] ?? 0;
    const successCount = eventsPosted - errorCount;

    // Postgres-formatted timestamps for SQL (trace_log uses timestamptz)
    const sPg = testStartTime.toISOString().replace("T", " ").replace("Z", "+00");
    const ePg = queryEndTime.toISOString().replace("T", " ").replace("Z", "+00");

    const lines = [
        "",
        "╔══════════════════════════════════════════════════════════════╗",
        "║         Constant-VUs Throughput Test — Summary               ║",
        "╚══════════════════════════════════════════════════════════════╝",
        "",
        "  k6 results (what was sent):",
        `    Events posted (HTTP 200) : ${successCount}`,
        `    Errors                   : ${errorCount}`,
        `    p(95) POST latency       : ${p95Ms.toFixed(0)} ms`,
        "",
        `  Run ID    : ${runId}${runId === "untagged" ? "  ⚠ pass -e runId=... to isolate this run" : ""}`,
        `  Event type: ${eventType}`,
        `  Query window: ${testStartTime.toISOString()} → ${queryEndTime.toISOString()}  (+${offsetMinutes} min offset)`,
        "  Override offset with -e queryOffsetMinutes=N",
        "",

        // ── SQL ──────────────────────────────────────────────────────
        "  ════════════════════════════════════════════════════════════",
        "  SQL — events.trace_log",
        "  ════════════════════════════════════════════════════════════",
        "",
        "  -- SQL 1: ingest vs outbound queue counts ───────────────────",
        "  SELECT",
        "      COUNT(*) FILTER (WHERE activity = 'Registered')         AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue')      AS events_queued,",
        "      COUNT(*) FILTER (WHERE activity = 'WebhookPostResponse') AS events_webhook_response,",
        "      COUNT(*) FILTER (WHERE activity = 'Unauthorized')       AS events_unauthorized",
        "  FROM events.trace_log",
        `  WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        `    AND eventtype = '${eventType}';`,
        "",
        "  -- SQL 2: throughput per 5s bucket ────────────────────────",
        "  SELECT",
        "      to_timestamp(floor(extract(epoch FROM time) / 5) * 5) AT TIME ZONE 'UTC' AS bucket,",
        "      COUNT(*) FILTER (WHERE activity = 'Registered')    AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue') AS events_queued,",
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

        // ── Cleanup SQL ───────────────────────────────────────────────
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
