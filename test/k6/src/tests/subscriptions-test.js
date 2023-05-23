/*
    Test script to platform subscriptions api with user token
    Command:
      docker-compose run k6 run /src/tests/subscriptions.js `
      -e env=*** `
      -e tokenGeneratorUserName=autotest `
      -e tokenGeneratorUserPwd=*** `
      -e app=apps-test `
      -e webhookEndpointToken=***** `
      -e runFullTestSet=true

    For use case tests omit environment variable runFullTestSet or set value to false
    */

import * as webhooksiteApi from "../api/webhooksite.js";
import { addErrorCount } from "../errorhandler.js";
import { generateJUnitXML, reportPath } from "../report.js";

export const options = {
  thresholds: {
    errors: ["count<1"],
  },
};

const org = "ttd";


export function setup() {
  console.log('setup running');
  var webhookToken = webhooksiteApi.createNewToken();

  console.log('webhookToken: '+ webhookToken);

  var data = {
    org: org
  };

  return data;
}

export default function (data) {
console.log('Default func');
}

/*
export function handleSummary(data) {
  let result = {};
  result[reportPath("events.xml")] = generateJUnitXML(data, "events");

  return result;
}
*/
