/*
    Test script to platform events api with user token
    Command:
    docker-compose run k6 run /src/tests/events/get.js `
    -e tokenGeneratorUserName=autotest `
    -e tokenGeneratorUserPwd=*** `
    -e mpClientId=*** `
    -e mpKid=altinn-usecase-events `
    -e encodedJwk=*** `
    -e altinn_env=*** `
    -e runFullTestSet=true `
    -e useCSVData=false
    --vus 10 `
    --duration 30s

    For running this as a single line command, the following can be used:
    "docker-compose run k6 run /src/tests/events/get.js -e altinn_env=*** -e tokenGeneratorUserName=*** -e tokenGeneratorUserPwd=*** -e useCSVData=true --vus 1 --duration 30s"

    For use case tests omit environment variable runFullTestSet or set value to false
    Set useCSVData=true to load test data from CSV file instead of JSON
    Update the variables --vus and --duration as needed for performance testing
*/
import { check } from "k6";
import * as setupToken from "../../setup.js";
import * as eventsApi from "../../api/events.js";
import { uuidv4 } from "https://jslib.k6.io/k6-utils/1.4.0/index.js";
import { addErrorCount, stopIterationOnFail } from "../../errorhandler.js";
import { getCommonOptions } from "../../config.js";
import { 
    loadCSV, 
    loadJSONDirectory, 
    loadJSON,
    createCloudEventFromCSV,
    getItemByVU 
} from "../../dataLoader.js";

const scopes = "altinn:events.subscribe altinn:serviceowner";

// Load test data using SharedArray for memory efficiency
// Choose between CSV or JSON based on environment variable
const useCSVData = (__ENV.useCSVData || "").toLowerCase() === "true";

const eventVariations = useCSVData
    ? loadCSV('event-variations', '../../data/events/event-variations.csv')
    : loadJSONDirectory('event-variations', '../../data/events/', [
        '01-event.json',
        '02-event.json',
        '03-event.json'
    ]);

const defaultEvent = loadJSON("../../data/events/01-event.json");

export const options = getCommonOptions({        
});

export function setup() {
    console.log(`1. Environment: ${__ENV.altinn_env}`);
    console.log(`2. Using CSV data: ${useCSVData}`);
    console.log(`3. Event variations count: ${eventVariations.length}`);
    
    let token = setupToken.getAltinnTokenForOrg(scopes);
    
    console.log(`4. Token received: ${token ? 'Yes' : 'No'}`);
    
    let cloudEvent = { ...defaultEvent };
    cloudEvent.id = uuidv4();

    const runFullTestSet = __ENV.runFullTestSet
        ? __ENV.runFullTestSet.toLowerCase().includes("true")
        : false;

    console.log(`5. Run full test set: ${runFullTestSet}`);
    
    // Seed events from CSV data to ensure they exist before GET tests
    console.log(`6. Seeding events for GET tests...`);
    let seededEvents = [];
    
    for (let i = 0; i < eventVariations.length; i++) {
        const eventData = eventVariations[i];
        let seedEvent;
        
        if (useCSVData) {
            seedEvent = createCloudEventFromCSV(eventData, { 
                id: uuidv4(),
                time: new Date().toISOString()
            });
        } else {
            seedEvent = { ...eventData };
            seedEvent.id = uuidv4();
            seedEvent.time = new Date().toISOString();
        }
        
        // Post the event to ensure it exists
        const response = eventsApi.postCloudEvent(JSON.stringify(seedEvent), token);
        if (response.status === 200 || response.status === 201) {
            seededEvents.push(seedEvent);
            console.log(`   Seeded event ${i + 1}: ${seedEvent.type} for ${seedEvent.subject}`);
        } else {
            console.log(`   Failed to seed event ${i + 1}: status ${response.status}`);
        }
    }
    
    console.log(`7. Seeded ${seededEvents.length} events`);

    if (seededEvents.length === 0) {
        throw new Error("Seeding failed: no events created; aborting GET tests.");
    }
    
    let data = {
        runFullTestSet: runFullTestSet,
        token: token,
        cloudEvent: cloudEvent,
        useCSVData: useCSVData,
        seededEvents: seededEvents,
    };

    console.log(`8. Setup data:`, JSON.stringify(data.cloudEvent));

    return data;
}

function TC01_GetAllEvents(data) {
    let response, success;
    
    response = eventsApi.getCloudEvents(
        {
            after: 0,
            resource: data.cloudEvent.resource,
            source: data.cloudEvent.source,
            type: data.cloudEvent.type,
            subject: data.cloudEvent.subject,
            size: 10,
        },
        data.token
    );
    success = check(response, {
        "GET all cloud events: status is 200": (r) => r.status === 200,
    });
    addErrorCount(success);

    if (!success) {
        stopIterationOnFail("GET all cloud events: status is 200", success, response);
    }

    success = check(response, {
        "GET all cloud events: at least 1 cloud event returned": (r) => {
            let responseBody = JSON.parse(r.body);
            console.log(`Console logging inside success response body: ${r.body}`);
            return Array.isArray(responseBody) && responseBody.length >= 1;
        },
    });

    addErrorCount(success);
}

function TC02_GetEventsAndFollowNextLink(data) {
    let response, success;

    response = eventsApi.getCloudEvents(
        {
            after: 0,
            source: data.cloudEvent.source,
            resource: data.cloudEvent.resource,
            type: data.cloudEvent.type,
            size: 1,
        },
        data.token
    );

    let nextUrl = response.headers["Next"];

    success = check(response, {
        "GET cloud events: status is 200": (r) => r.status === 200,
        "GET cloud events: next link is provided ": (r) => nextUrl,
    });
    addErrorCount(success);

    response = eventsApi.getEventsFromNextLink(nextUrl, data.token);
    success = check(response, {
        "GET cloud events from next link: status is 200": (r) => r.status === 200,
    });

    addErrorCount(success);
}

function getEventForIteration(data) {
    // Use seeded events if available to ensure we query for events that actually exist
    if (data.seededEvents && data.seededEvents.length > 0) {
        const index = (__VU - 1) % data.seededEvents.length;
        const seededEvent = data.seededEvents[index];
        console.log(`VU ${__VU}: Using seeded event ${index + 1}:`, JSON.stringify(seededEvent));
        return seededEvent;
    }
    
    // Fallback to creating new events if no seeded events available
    const eventData = getItemByVU(eventVariations, __VU);
    
    console.log(`VU ${__VU}: Event data:`, JSON.stringify(eventData));
    
    let cloudEvent;
    if (data.useCSVData) {
        cloudEvent = createCloudEventFromCSV(eventData, { 
            id: uuidv4(),
            time: new Date().toISOString()
        });
    } else {
        cloudEvent = { ...eventData };
        cloudEvent.id = uuidv4();
        cloudEvent.time = new Date().toISOString();
    }
    
    // POST the event so GET tests can find it
    const response = eventsApi.postCloudEvent(JSON.stringify(cloudEvent), data.token);
    if (response.status !== 200 && response.status !== 201) {
        console.warn(`VU ${__VU}: Failed to create fallback event: ${response.status}`);
    }
    
    console.log(`VU ${__VU}: Cloud event:`, JSON.stringify(cloudEvent));
    
    return cloudEvent;
}

/*
 * 01 - GET all existing cloud events for subject /party/1337
 * 02 - GET events and follow next link
 */
export default function runTests(data) {
    try {
        const iterationEvent = getEventForIteration(data);
        
        const testData = {
            ...data,
            cloudEvent: iterationEvent
        };
        
        if (data.runFullTestSet) {
            TC01_GetAllEvents(testData);
            TC02_GetEventsAndFollowNextLink(testData);
        } else {
            TC01_GetAllEvents(testData);
        }
    } catch (error) {
        addErrorCount(false);
        throw error;
    }
}
