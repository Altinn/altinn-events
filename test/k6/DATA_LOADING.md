# Test Data Loading Guide

This guide explains how to use the data loading capabilities in k6 tests.

## Overview

The data loading system provides memory-efficient ways to load test data from various sources using k6's SharedArray functionality. This ensures that large datasets are loaded once and shared across all Virtual Users (VUs).

## Files Created

### 1. Data Loader Module (`src/dataLoader.js`)
A comprehensive utility module with functions for:
- Loading CSV files
- Loading JSON files (single or multiple)
- Creating cloud events from CSV data
- Selecting data by VU or iteration
- Getting random data items

### 2. Sample Data Files

#### CSV Format (`src/data/events/event-variations.csv`)
Contains 7 different event variations in CSV format with columns:
- source
- specversion
- type
- subject
- resource
- alternativeSubject

#### JSON Format
- `01-event.json` - Original event (preserved for backward compatibility)
- `02-event.json` - Order creation event
- `03-event.json` - Payment completed event

## Usage Examples

### Basic Usage (Current Implementation)

The `get.js` test now supports both CSV and JSON data loading:

```bash
# Use JSON files (default)  
 docker-compose run k6 run /src/tests/events/get.js \  
   -e altinn_env=*** \  
   -e tokenGeneratorUserName=autotest \  
   -e runFullTestSet=true  

 # Use CSV file  
 docker-compose run k6 run /src/tests/events/get.js \  
   -e altinn_env=*** \  
   -e tokenGeneratorUserName=autotest \  
   -e runFullTestSet=true \  
   -e useCSVData=true  
```

### Loading CSV Data

```javascript
import { loadCSV, createCloudEventFromCSV } from '../../dataLoader.js';

// Load CSV file
const events = loadCSV('events', '../../data/events/event-variations.csv');

// Use in test
export default function() {
    const csvRow = events[(__VU - 1) % events.length];
    const cloudEvent = createCloudEventFromCSV(csvRow, { id: uuidv4() });
    // Use cloudEvent...
}
```

### Loading Multiple JSON Files

```javascript
import { loadJSONDirectory, getItemByVU } from '../../dataLoader.js';

// Load all JSON files
const events = loadJSONDirectory('events', '../../data/events/', [
    '01-event.json',
    '02-event.json',
    '03-event.json'
]);

// Get event for this VU (round-robin)
export default function() {
    const event = getItemByVU(events, __VU);
    // Use event...
}
```

### Loading Single JSON File

```javascript
import { loadJSON } from '../../dataLoader.js';

const config = loadJSON('../../data/config.json');
```

### Random Selection

```javascript
import { loadCSV, getRandomItem } from '../../dataLoader.js';

const events = loadCSV('events', '../../data/events/event-variations.csv');

export default function() {
    // Get random event each iteration
    const randomEvent = getRandomItem(events);
    // Use randomEvent...
}
```

### Iteration-Based Selection

```javascript
import { loadJSONDirectory, getItemByIteration } from '../../dataLoader.js';

const events = loadJSONDirectory('events', '../../data/events/', ['01-event.json', '02-event.json']);

export default function() {
    // Different event each iteration
    const event = getItemByIteration(events, __ITER);
    // Use event...
}
```

## Data Loader API Reference

### loadCSV(name, filePath, options)
Loads and parses a CSV file into a SharedArray.
- **name**: Unique identifier for the SharedArray
- **filePath**: Relative path to CSV file
- **options**: (Optional) Parsing options — `{ delimiter: ',', skipEmptyLines: true }`
- **Returns**: SharedArray of objects

### loadJSON(filePath)
Loads a single JSON file.
- **filePath**: Relative path to JSON file
- **Returns**: Parsed JSON object

### loadJSONFiles(name, filePaths)
Loads multiple JSON files into a SharedArray.
- **name**: Unique identifier for the SharedArray
- **filePaths**: Array of relative paths
- **Returns**: SharedArray of objects

### loadJSONDirectory(name, baseDir, fileNames)
Loads multiple JSON files from a directory.
- **name**: Unique identifier for the SharedArray
- **baseDir**: Base directory path
- **fileNames**: Array of file names
- **Returns**: SharedArray of objects

### createCloudEventFromCSV(csvRow, overrides)
Creates a cloud event object from CSV row data.
- **csvRow**: Object representing a CSV row
- **overrides**: Optional fields to add/override
- **Returns**: Cloud event object

### getRandomItem(array)
Gets a random item from an array.
- **array**: Source array
- **Returns**: Random item

### getItemByVU(array, vuId)
Gets an item based on VU number (round-robin).
- **array**: Source array
- **vuId**: Virtual User ID (use `__VU`)
- **Returns**: Item from array

### getItemByIteration(array, iteration)
Gets an item based on iteration number.
- **array**: Source array
- **iteration**: Iteration number (use `__ITER`)
- **Returns**: Item from array

## Benefits

### Memory Efficiency
Using SharedArray means data is loaded once and shared across all VUs, reducing memory usage significantly in high-load tests.

### Flexibility
- Switch between CSV and JSON formats
- Easy to add new test data variations
- Support for random, round-robin, or sequential data selection

### Scalability
- Handle large datasets efficiently
- Distribute different test scenarios across VUs
- Easy to extend with new data sources

## Adding New Test Data

### Adding CSV Data
1. Edit `src/data/events/event-variations.csv`
2. Add new rows with your test data
3. No code changes needed!

### Adding JSON Files
1. Create new JSON file (e.g., `04-event.json`)
2. Update the test to include it:
```javascript
const events = loadJSONDirectory('events', '../../data/events/', [
    '01-event.json',
    '02-event.json',
    '03-event.json',
    '04-event.json'  // Add your new file
]);
```

## Best Practices

1. **Use CSV for large datasets**: More compact and easier to edit
2. **Use JSON for complex structures**: Better for nested data
3. **Use SharedArray**: Always use SharedArray for data shared across VUs
4. **Distribute load**: Use `getItemByVU()` to ensure even distribution
5. **Add variation**: More data variations = more realistic load tests

## Performance Tips

- CSV files are more memory-efficient than JSON for tabular data
- SharedArray prevents data duplication across VUs
- Pre-generate dynamic values (like timestamps) in the data file when possible
- Use round-robin selection (`getItemByVU`) for predictable distribution
- Use random selection (`getRandomItem`) for more realistic scenarios
