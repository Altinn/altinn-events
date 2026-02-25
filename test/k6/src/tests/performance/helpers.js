import { check } from "k6";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import * as subscriptionsApi from "../../api/subscriptions.js";
import * as config from "../../config.js";
import { addErrorCount } from "../../errorhandler.js";

export const scopes = "altinn:events.publish altinn:serviceowner altinn:events.subscribe";
export const resourceFilter = "urn:altinn:resource:ttd-altinn-events-automated-tests";
export const runId = __ENV.runId || "untagged";

export function warnIfUntagged() {
    if (runId === "untagged") {
        console.warn(`[WARN] No runId provided. Pass -e runId=$(date +%Y%m%d-%H%M%S) to isolate this run.`);
    }
}

export function performanceSetup(setupLogLine) {
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
    if (setupLogLine) {
        console.log(`[SETUP] ${setupLogLine}`);
    }

    return { token, subscriptionId };
}

export function postCloudEvent(eventType, token) {
    const cloudEvent = {
        id: uuidv4(),
        source: "https://github.com/Altinn/altinn-events/tree/main/test/k6",
        specversion: "1.0",
        type: eventType,
        subject: "/autotest/k6",
        resource: resourceFilter,
        time: new Date().toISOString(),
    };

    const response = eventsApi.postCloudEvent(JSON.stringify(cloudEvent), token);

    const success = check(response, {
        "POST event. Status is 200": (r) => r.status === 200,
    });

    addErrorCount(success);
}

export function performanceTeardown(data) {
    if (data.subscriptionId) {
        console.log(`[TEARDOWN] Subscription ${data.subscriptionId} left active — delete manually when done.`);
    }
}

export function getTimeWindow(data) {
    const testEndTime    = new Date();
    const testDurationMs = data.state.testRunDurationMs;
    const testStartTime  = new Date(testEndTime.getTime() - testDurationMs);

    const offsetMinutes = Number.parseInt(__ENV.queryOffsetMinutes || "15", 10);
    const queryEndTime  = new Date(testEndTime.getTime() + offsetMinutes * 60 * 1000);

    const durationSec = testDurationMs / 1000;

    const sPg = testStartTime.toISOString().replace("T", " ").replace("Z", "+00");
    const ePg = queryEndTime.toISOString().replace("T", " ").replace("Z", "+00");

    return { testStartTime, testEndTime, queryEndTime, offsetMinutes, durationSec, sPg, ePg };
}

export function getBaseMetrics(data) {
    const eventsPosted = data.metrics["http_reqs"]?.values?.count ?? 0;
    const errorCount   = data.metrics["error_rate"]?.values?.count ?? 0;
    const p95Ms        = data.metrics["http_req_duration"]?.values?.["p(95)"] ?? 0;
    const successCount = eventsPosted - errorCount;

    return { eventsPosted, errorCount, p95Ms, successCount };
}

export function buildMetaLines(eventType, testStartTime, queryEndTime, offsetMinutes) {
    return [
        `  Run ID    : ${runId}${runId === "untagged" ? "  ⚠ pass -e runId=... to isolate this run" : ""}`,
        `  Event type: ${eventType}`,
        `  Query window: ${testStartTime.toISOString()} → ${queryEndTime.toISOString()}  (+${offsetMinutes} min offset)`,
        "  Override offset with -e queryOffsetMinutes=N",
    ];
}

export function buildSqlSection(eventType, sPg, ePg) {
    return [
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
        "  -- SQL 3: actual rate per 5s bucket ────────────────────────",
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
    ];
}

export function buildSubscriptionWarning(testEndTime, queryEndTime) {
    return [
        `  Test ended at: ${testEndTime.toISOString()}`,
        `  Query window ends at: ${queryEndTime.toISOString()}`,
        "",
        "  ⚠  Subscription was NOT deleted — webhooks are still delivering.",
        "     Check [SETUP] log above for the subscription ID, then delete",
        "     it manually once SQL 1 shows events_webhook_response ≈ events_queued.",
    ];
}

export function buildCleanupSection(eventType, sPg, ePg) {
    return [
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
    ];
}

export function buildSummary(title, k6ResultLines, eventType, data) {
    const tw = getTimeWindow(data);
    const lines = [
        "",
        "╔══════════════════════════════════════════════════════════════╗",
        `║         ${title}`,
        "╚══════════════════════════════════════════════════════════════╝",
        "",
        ...k6ResultLines,
        "",
        ...buildMetaLines(eventType, tw.testStartTime, tw.queryEndTime, tw.offsetMinutes),
        "",
        "",
        ...buildSqlSection(eventType, tw.sPg, tw.ePg),
        "",
        ...buildSubscriptionWarning(tw.testEndTime, tw.queryEndTime),
        "",
        "",
        ...buildCleanupSection(eventType, tw.sPg, tw.ePg),
        "",
    ];

    return { stdout: lines.join("\n") + "\n" };
}
