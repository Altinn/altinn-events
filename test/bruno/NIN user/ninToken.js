const btoa = require("btoa");

const ninUsers = {
  "17902349936": {
    at22: {
      AuthN_UserId: "20885478",
      AuthN_PartyId: "51118947",
      AuthN_PartyUuid: "b8a3981b-9948-4109-88a9-679d90c4ab37"
    },
    at23: {
      AuthN_UserId: "20462603",
      AuthN_PartyId: "50891883",
      AuthN_PartyUuid: "629fa2c0-27cd-40a2-ac6a-99bdf374dba2"
    },
    tt02: {
      AuthN_UserId: "1597896",
      AuthN_PartyId: "51368167",
      AuthN_PartyUuid: "200718e3-ab43-4d34-86ee-4cd8a975a55c"
    }
  }
};

const obtainToken = async (nin, scopes, force = false) => {

  if (!nin) {
    throw new Error("A national identification number (nin) is required by this token generator script.");
  }

  const environment = bru.getEnvVar("Environment");
  if (!environment) {
    throw new Error("An environment value is required by this token generator script.");
  }

  const tokenScopes = scopes ?? "altinn:portal/enduser"; 

  // Check if existing token can be reused
  const nowDate = Date.now();
  const tokenDate = bru.getVar("t_tokenDate") ?? 0;
  
  const currentTokenParameters = environment + nin + tokenScopes;
  const previousTokenParameters = bru.getVar("t_tokenParameters") ?? "";
  
  if (!force && currentTokenParameters == previousTokenParameters && tokenDate + (1000 * 60 * 25) > nowDate) { // 25 minutes
    console.log("The existing token should still be valid. Not making a new one.");
    return false;
  }
  
  if (!ninUsers[nin]) {
    throw new Error(`No configured nin user with pid: ${nin}. Check the ninUsers object in ninToken.js file.`);
  }
  
  const user = ninUsers[nin][environment];
  
  if (!user) {
    throw new Error(`Could not find environment "${environment}" for user "${nin}". Check the ninUsers object in ninToken.js file.`);
  }
  
  // Generate a new token
  const tokenGeneratorBaseUrl = bru.getProcessEnv("TOKEN_URL");
  const tokenGeneratorUserName = bru.getProcessEnv("TOKEN_BASIC_AUTH_USER");
  const tokenGeneratorUserPassword = bru.getProcessEnv("TOKEN_BASIC_AUTH_PW");
  
  let tokenUrl = `${tokenGeneratorBaseUrl}/api/GetPersonalToken?env=${environment}&scopes=${tokenScopes}&pid=${nin}&userid=${user.AuthN_UserId}&partyid=${user.AuthN_PartyId}&authLvl=3&ttl=1800`;
  
  const authorizationHeader = 'Basic ' + btoa(`${tokenGeneratorUserName}:${tokenGeneratorUserPassword}`);
  const response = await bru.sendRequest({
    method: 'GET',
    url: tokenUrl,
    headers: {
      'Authorization': authorizationHeader
    }
  });
  
  // Store the new token and some metadata for future reuse
  bru.setVar("t_token", response.data);
  bru.setVar("t_tokenDate", nowDate);
  bru.setVar("t_tokenParameters", currentTokenParameters);
  
  console.log("A new token has been successfully obtained.");
  
  // Set variables for use in requests.
  bru.setVar("AuthN_Pid", nin);
  bru.setVar("AuthN_UserId", user.AuthN_UserId);
  bru.setVar("AuthN_PartyId", user.AuthN_PartyId);
  bru.setVar("AuthN_PartyUuid", user.AuthN_PartyUuid);
};

module.exports = {
  obtainToken
};
