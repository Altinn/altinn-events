/**
 * Data Loader Module for k6 Tests
 * 
 * Provides utilities for loading test data from various sources (CSV, JSON)
 * using SharedArray for memory-efficient data sharing across VUs.
 */

import { SharedArray } from 'k6/data';

/**
 * Parse CSV string into array of objects
 * Local implementation - no external dependencies required
 * Optimized for O(n) time complexity where n is the number of rows
 * 
 * @param {string} csvString - Raw CSV content
 * @param {Object} options - Parsing options
 * @param {string} options.delimiter - Field delimiter (default: ',')
 * @param {boolean} options.skipEmptyLines - Skip empty lines (default: true)
 * @returns {Array<Object>} Array of objects with CSV data
 */
function parseCSV(csvString, options = {}) {
    const delimiter = options.delimiter || ',';
    const skipEmptyLines = options.skipEmptyLines !== false;
    
    // Split into lines and clean in one pass
    const lines = csvString.split('\n');
    
    if (lines.length === 0) {
        return [];
    }
    
    const headerLine = lines[0].replace('\r', '').trim();
    if (!headerLine) {
        return [];
    }
    
    const headers = headerLine.split(delimiter);
    const headerCount = headers.length;
     
    for (let i = 0; i < headerCount; i++) {
        headers[i] = headers[i].trim();
    }
    
    const result = [];
    
    for (let i = 1; i < lines.length; i++) {
        const line = lines[i].replace('\r', '');
               
        if (skipEmptyLines && !line.trim()) {
            continue;
        }
        
        if (line.includes('"')) {
            throw new Error('parseCSV: quoted fields are not supported by this parser');
        }
        const values = line.split(delimiter);
        
        let hasData = false;
        const row = {};
        
        for (let j = 0; j < headerCount; j++) {
            const value = values[j] ? values[j].trim() : '';
            row[headers[j]] = value;
            if (value) {
                hasData = true;
            }
        }
        
        if (hasData) {
            result.push(row);
        }
    }
    
    return result;
}

/**
 * Load CSV file and parse into array of objects
 * Uses SharedArray to share data efficiently between Virtual Users
 * 
 * @param {string} name - Unique name for this SharedArray
 * @param {string} filePath - Path to CSV file relative to test file
 * @param {Object} options - Parsing options (delimiter, skipEmptyLines)
 * @returns {SharedArray} Array of objects with CSV data
 * 
 * @example
 * const events = loadCSV('events', '../../data/events/event-variations.csv');
 * const event = events[(__VU - 1) % events.length]; // Get event for this VU
 */
export function loadCSV(name, filePath, options = {}) {
    return new SharedArray(name, function() {
        const csvContent = open(filePath);
        return parseCSV(csvContent, options);
    });
}

/**
 * Load multiple JSON files and combine into a SharedArray
 * 
 * @param {string} name - Unique name for this SharedArray
 * @param {Array<string>} filePaths - Array of paths to JSON files
 * @returns {SharedArray} Array of parsed JSON objects
 * 
 * @example
 * const events = loadJSONFiles('events', [
 *     '../../data/events/01-event.json',
 *     '../../data/events/02-event.json'
 * ]);
 */
export function loadJSONFiles(name, filePaths) {
    return new SharedArray(name, function() {
        return filePaths.map(filePath => JSON.parse(open(filePath)));
    });
}

/**
 * Load a single JSON file
 * 
 * @param {string} filePath - Path to JSON file
 * @returns {Object} Parsed JSON object
 * 
 * @example
 * const event = loadJSON('../../data/events/01-event.json');
 */
export function loadJSON(filePath) {
    return JSON.parse(open(filePath));
}

/**
 * Load all JSON files from a directory pattern
 * Useful for loading all event variations automatically
 * 
 * @param {string} name - Unique name for this SharedArray
 * @param {string} baseDir - Base directory path
 * @param {Array<string>} fileNames - Array of file names to load
 * @returns {SharedArray} Array of parsed JSON objects
 * 
 * @example
 * const events = loadJSONDirectory('events', '../../data/events/', 
 *     ['01-event.json', '02-event.json', '03-event.json']);
 */
export function loadJSONDirectory(name, baseDir, fileNames) {
    return new SharedArray(name, function() {
        return fileNames.map(fileName => {
            const filePath = baseDir + fileName;
            return JSON.parse(open(filePath));
        });
    });
}

/**
 * Get a random item from an array
 * Useful for selecting random test data
 * 
 * @param {Array} array - Array to select from
 * @returns {*} Random item from array
 * 
 * @example
 * const event = getRandomItem(events);
 */
export function getRandomItem(array) {
    if (!array || array.length === 0) {
        throw new Error('getRandomItem: array is empty');
    }
    return array[Math.floor(Math.random() * array.length)];
}
/**
 * Get an item from array based on VU number (round-robin)
 * Ensures even distribution across VUs
 * 
 * @param {Array} array - Array to select from
 * @param {number} vuId - Virtual User ID (typically __VU)
 * @returns {*} Item from array
 * 
 * @example
 * const event = getItemByVU(events, __VU);
 */
export function getItemByVU(array, vuId) {
    if (!array || array.length === 0) {
        throw new Error('getItemByVU: array is empty');
    }
    return array[(vuId - 1) % array.length];
}

/**
 * Get an item from array based on iteration number (round-robin)
 *
 * @param {Array} array - Array to select from
 * @param {number} iteration - Iteration number (typically __ITER)
 * @returns {*} Item from array
 *
 * @example
 * const event = getItemByIteration(events, __ITER);
 */
export function getItemByIteration(array, iteration) {
    if (!array || array.length === 0) {
        throw new Error('getItemByIteration: array is empty');
    }
    return array[iteration % array.length];
}

/**
 * Create a cloud event from CSV row data
 * Adds dynamic fields like timestamp and ID
 * 
 * @param {Object} csvRow - Row data from CSV
 * @param {Object} overrides - Optional fields to override
 * @returns {Object} Cloud event object
 * 
 * @example
 * const event = createCloudEventFromCSV(csvRow, { id: uuidv4() });
 */
export function createCloudEventFromCSV(csvRow, overrides = {}) {
    const event = {
        source: csvRow.source,
        specversion: csvRow.specversion || '1.0',
        type: csvRow.type,
        subject: csvRow.subject,
        resource: csvRow.resource,
    };
    
    // Note: alternativeSubject from CSV is intentionally excluded as it's not a valid CloudEvent attribute
    // Standard attributes only: source, specversion, type, subject, id, time, datacontenttype, dataschema
    
    // Add timestamp if not provided
    if (!overrides.time) {
        event.time = new Date().toISOString();
    }
    
    // Apply overrides
    return { ...event, ...overrides };
}

/**
 * Create a subscription object from CSV row data
 * Maps CSV columns to subscription properties
 * 
 * @param {Object} csvRow - Row data from CSV
 * @param {Object} overrides - Optional fields to override
 * @returns {Object} Subscription object
 * 
 * @example
 * const subscription = createSubscriptionFromCSV(csvRow, { 
 *     endPoint: 'https://webhook.site/xxx' 
 * });
 */
export function createSubscriptionFromCSV(csvRow, overrides = {}) {
    const subscription = {
        endPoint: csvRow.endPoint,
    };
    
    // Add optional subscription fields if they exist in CSV
    if (csvRow.sourceFilter && csvRow.sourceFilter.trim() !== '') {
        subscription.sourceFilter = csvRow.sourceFilter;
    }
    
    if (csvRow.typeFilter && csvRow.typeFilter.trim() !== '') {
        subscription.typeFilter = csvRow.typeFilter;
    }
    
    if (csvRow.subjectFilter && csvRow.subjectFilter.trim() !== '') {
        subscription.subjectFilter = csvRow.subjectFilter;
    }
    
    if (csvRow.resourceFilter && csvRow.resourceFilter.trim() !== '') {
        subscription.resourceFilter = csvRow.resourceFilter;
    }
    
    if (csvRow.consumer && csvRow.consumer.trim() !== '') {
        subscription.consumer = csvRow.consumer;
    }
    
    // Apply overrides (e.g., for dynamic endpoint or filters)
    return { ...subscription, ...overrides };
}