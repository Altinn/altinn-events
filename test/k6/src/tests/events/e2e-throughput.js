/*
    Performance test: end-to-end event throughput (registration → outbound queue)

    A single subscription is created once in setup(). The main loop then posts events
    as fast as possible for the configured duration. Every posted event that matches
    the subscription triggers the full outbound pipeline, ending with the event being
    enqueued on the outbound Service Bus queue (TraceLogActivity.OutboundQueue).

    After the test, handleSummary() prints k6 results and ready-to-run SQL queries
    against the trace_log table with the exact test timestamps already filled in.

    Command:
    docker-compose run k6 run /src/tests/events/e2e-throughput.js `
      -e altinn_env=yt01 `
      -e tokenGeneratorUserName=autotest `
      -e tokenGeneratorUserPwd=*** `
      -e runId=$(date +%Y%m%d-%H%M%S) \
      --vus 10 `
      --duration 5m

    queryOffsetMinutes controls how far past the test end the queries look.
    Increase if outbound queue processing is expected to lag (default: 15).
    -e queryOffsetMinutes=30

    Pass a runId to isolate this run's trace_log rows from concurrent traffic.
    __ENV variables are consistent across all VUs (unlike module-level code which
    runs once per VU). Without runId, all events share the type
    "automatedtest.e2e-throughput.untagged" — fine for solo runs, ambiguous otherwise.
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
const runId = __ENV.runId || "untagged";
const eventType = `automatedtest.e2e-throughput.${runId}`;

if (runId === "untagged") {
    console.warn("[WARN] No runId provided. Events will use type 'automatedtest.e2e-throughput.untagged'.");
    console.warn("[WARN] Pass -e runId=$(date +%Y%m%d-%H%M%S) to isolate this run in trace_log queries.");
}

export const options = {
    thresholds: {
        error_rate: ["count<1"],
        http_req_duration: ["p(95)<5000"],
    },
    // Defaults — override via --vus / --duration on the CLI
    vus: 10,
    duration: "5m",
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
// Removes the subscription so it does not accumulate across runs.
export function teardown(data) {
    if (!data.subscriptionId) {
        return;
    }

    const token = setupToken.getAltinnTokenForOrg(scopes);
    const response = subscriptionsApi.deleteSubscription(data.subscriptionId, token);

    check(response, {
        "Teardown: subscription deleted. Status is 200": (r) => r.status === 200,
    });

    console.log(`[TEARDOWN] Deleted subscription ${data.subscriptionId}`);
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
        "║           E2E Throughput Test — Summary                      ║",
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
        "      COUNT(*) FILTER (WHERE activity = 'Registered')    AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue') AS events_queued,",
        "      COUNT(*) FILTER (WHERE activity = 'Unauthorized')  AS events_unauthorized",
        "  FROM events.trace_log",
        `  WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        `    AND eventtype = '${eventType}';`,
        "",
        "  -- SQL 2: throughput per 10s bucket ────────────────────────",
        "  SELECT",
        "      to_timestamp(floor(extract(epoch FROM time) / 10) * 10) AT TIME ZONE 'UTC' AS bucket,",
        "      COUNT(*) FILTER (WHERE activity = 'Registered')                            AS events_registered,",
        "      COUNT(*) FILTER (WHERE activity = 'OutboundQueue')                         AS events_queued",
        "  FROM events.trace_log",
        `  WHERE time BETWEEN '${sPg}' AND '${ePg}'`,
        "    AND activity IN ('Registered', 'OutboundQueue')",
        `    AND eventtype = '${eventType}'`,
        "  GROUP BY bucket",
        "  ORDER BY bucket;",
        "",
        "  -- SQL 3: end-to-end latency (register → outbound queue) ────",
        "  -- Joins on cloudeventid so each row is one complete trip.",
        "  -- p95/p99 show where the pipeline is slow under load.",
        "  SELECT",
        "      COUNT(*)                                                                                              AS total_events,",
        "      ROUND(AVG(EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))                                             AS avg_latency_ms,",
        "      ROUND(PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))   AS p50_ms,",
        "      ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))   AS p95_ms,",
        "      ROUND(PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))   AS p99_ms,",
        "      ROUND(MIN(EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))                                             AS min_ms,",
        "      ROUND(MAX(EXTRACT(EPOCH FROM (q.time - r.time)) * 1000))                                             AS max_ms",
        "  FROM events.trace_log r",
        "  JOIN events.trace_log q ON r.cloudeventid = q.cloudeventid",
        `  WHERE r.time BETWEEN '${sPg}' AND '${ePg}'`,
        "    AND r.activity = 'Registered'",
        `    AND r.eventtype = '${eventType}'`,
        "    AND q.activity = 'OutboundQueue';",
        "",
        `  Test ended at: ${testEndTime.toISOString()}`,
        "",
    ];

    return { stdout: lines.join("\n") + "\n" };
}
