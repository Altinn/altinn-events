// Central place to define all test users and organizations per environment.
// This file can be required from Bruno scripts, e.g. in portal user.bru.

const emailUsers = {
  "terjedatatest@leet.no": {
    at22: {
        AuthN_UserId: "21228824",
        AuthN_PartyId: "51845134",
        AuthN_PartyUuid: "ea9d9208-8a15-4a1d-8230-0cc22ed89dff"
    },
    at23: {
        AuthN_UserId: "21168915",
        AuthN_PartyId: "51782289",
        AuthN_PartyUuid: "e21fff6d-1553-48ed-b81d-3f7578af5937"
    },
    tt02: {
        AuthN_UserId: "2680415",
        AuthN_PartyId: "53440875",
        AuthN_PartyUuid: "9863acec-7893-4941-96ac-9b771b00fc62"
    }
  }
};

const obtainToken = async (email, scopes, force = false) => {

  if (!email) {
    throw new Error("An email is required by this token generator script.");
  }

  const environment = bru.getEnvVar("Environment");
  if (!environment) {
    throw new Error("An environment value is required by this token generator script.");
  }

  const tokenScopes = scopes ?? "altinn:portal/enduser"; 

  // Check if existing token can be reused
  const nowDate = Date.now();
  const tokenDate = bru.getVar("t_tokenDate") ?? 0;
  
  const currentTokenParameters = environment + email + tokenScopes;
  const previousTokenParameters = bru.getVar("t_tokenParameters") ?? "";
  
  if (!force && currentTokenParameters == previousTokenParameters && tokenDate + (1000 * 60 * 25) > nowDate) { // 25 minutes
    console.log("The existing token should still be valid. Not making a new one.");
    return false;
  }
  
  if (!emailUsers[email]) {
    throw new Error(`No configured email user with email: ${email}. Check the emailUsers object in emailUsers.js file.`);
  }
  
  const user = emailUsers[email][environment];
  
  if (!user) {
    throw new Error(`Could not find environment "${environment}" for user "${email}". Check the emailUsers object in emailUsers.js file.`);
  }
  
  // Generate a new token
  const tokenGeneratorBaseUrl = bru.getProcessEnv("TOKEN_URL");
  const tokenGeneratorUserName = bru.getProcessEnv("TOKEN_BASIC_AUTH_USER");
  const tokenGeneratorUserPassword = bru.getProcessEnv("TOKEN_BASIC_AUTH_PW");
  
  let tokenUrl = `${tokenGeneratorBaseUrl}/api/GetSelfRegisteredEmailUserToken?env=${environment}&scopes=${tokenScopes}&email=${email}&userid=${user.AuthN_UserId}&partyid=${user.AuthN_PartyId}&authLvl=3&ttl=1800`;
  
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
  bru.setVar("AuthN_Email", email);
  bru.setVar("AuthN_UserId", user.AuthN_UserId);
  bru.setVar("AuthN_PartyId", user.AuthN_PartyId);
  bru.setVar("AuthN_PartyUuid", user.AuthN_PartyUuid);
};

module.exports = {
  obtainToken
};
