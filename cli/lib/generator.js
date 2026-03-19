const axios = require('axios');
const cfg = require('./config');

const USINGS = `using FMS_Fabric_Putway_Api.Models;
using SAP.Middleware.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Vendor_Application_MVC.Controllers;
using Vendor_SRM_Routing_Application.Models.HU_Creation;
using Vendor_SRM_Routing_Application.Models.PeperlessPicklist;`;

const NAMESPACE = `Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist`;

/**
 * Generate the ASP.NET C# controller for an RFC spec
 * @param {Object} rfcSpec - parsed RFC spec from parser.js
 * @param {string} sapEnv - 'dev' | 'quality' | 'production'
 * @returns {string} C# controller code
 */
async function generateController(rfcSpec, sapEnv = 'dev') {
  const env = cfg.sap.environments[sapEnv];
  const importBlock = rfcSpec.importParams?.length
    ? rfcSpec.importParams.map(p => `- ${p.name} (SAP TYPE: ${p.sapType})`).join('\n')
    : '- (none)';

  const outputDesc = rfcSpec.outputType === 'table'
    ? `Returns TABLE parameter "${rfcSpec.outputTableName}" as an array. Use dynamic metadata loop (skip STRUCTURE/TABLE type fields). Response shape: {Status, Message, Data:{${rfcSpec.outputTableName}:[...rows]}}`
    : `Returns EX_RETURN only. Response shape: {Status, Message}`;

  const prompt = `You are a senior C# .NET Web API developer for V2 Retail's SAP RFC REST API project.

Generate a COMPLETE, production-ready ASP.NET Web API controller (.NET 4.7.2) for the following SAP RFC.

## RFC Specification
- RFC Name: ${rfcSpec.rfcName}
- Description: ${rfcSpec.description}
- SAP Connection Method: BaseController.${env.fn}
- SAP System: ${env.host} / Client ${env.client}

## IMPORT Parameters (caller sends these in the POST body)
${importBlock}

## Output
${outputDesc}

## STRICT CODE REQUIREMENTS
1. Use EXACTLY these using statements (copy verbatim):
${USINGS}

2. Namespace MUST be: namespace ${NAMESPACE}

3. Class name: ${rfcSpec.rfcName}Controller

4. Inherits from BaseController

5. Single POST endpoint: [HttpPost] [Route("api/${rfcSpec.rfcName}")]

6. Request model class at the bottom of the file — fields match the import params exactly

7. Standard error handling:
   - Catch RfcAbapException → return {Status:"E", Message: ex.Message}
   - Catch CommunicationException → return {Status:"E", Message: "SAP connection failed: " + ex.Message}
   - Catch Exception → return {Status:"E", Message: ex.Message}

8. EX_RETURN check: after RfcFunction.Invoke(destination), read EX_RETURN — if TYPE=="E" return error

9. For table output: use rfcFunction.GetTable("${rfcSpec.outputTableName || 'ET_DATA'}"), loop rows with dynamic metadata, skip fields of type STRUCTURE/TABLE

10. Return Ok(new { Status = "S", Message = "Success", Data = new { ${rfcSpec.outputTableName || 'ET_DATA'} = rows } })

Return ONLY the raw C# code. No markdown. No explanation.`;

  const res = await axios.post('https://api.anthropic.com/v1/messages', {
    model: cfg.anthropic.model,
    max_tokens: cfg.anthropic.maxTokens,
    messages: [{ role: 'user', content: prompt }]
  }, {
    headers: { 'Content-Type': 'application/json', 'x-api-key': process.env.ANTHROPIC_API_KEY || '' }
  });

  const raw = res.data.content?.find(b => b.type === 'text')?.text || '';
  return raw.replace(/```(?:csharp|cs)?\n?/g, '').replace(/```$/g, '').trim();
}

/**
 * Generate the .NET 4.7.2 data pipeline console app for this RFC
 */
async function generatePipeline(rfcSpec, sapEnv = 'dev') {
  const env = cfg.sap.environments[sapEnv];
  const sqlTable = rfcSpec.suggestedSqlTable || `ET_${rfcSpec.rfcName.replace(/_RFC$/,'')}`;
  const importBlock = rfcSpec.importParams?.map(p => `- ${p.name} (${p.sapType})`).join('\n') || '(none)';

  const prompt = `You are a senior C# developer. Generate a complete .NET 4.7.2 Console Application (Program.cs) that:

1. Connects to SAP using SAP NCo 3.0 (sapnco.dll) via these credentials:
   - Host: ${env.host}, SystemNumber: 00, Client: ${env.client}
   - User: ${cfg.sap.user}, Password: ${cfg.sap.password}, Language: EN

2. Calls RFC: ${rfcSpec.rfcName}
   Import params: ${importBlock}
   (Accept optional command-line args for date ranges: args[0]=from_date, args[1]=to_date — format YYYYMMDD)

3. Reads the output table: ${rfcSpec.outputTableName || 'ET_DATA'}

4. Auto-creates SQL Server table if not exists (table name: dbo.${sqlTable}):
   - Detect columns from RFC metadata at runtime
   - Map CHAR/NUMC → NVARCHAR(255), DATS/TIMS → NVARCHAR(20), CURR/DEC → DECIMAL(18,4), INT4/INT2 → INT, else NVARCHAR(255)
   - Always add: ID INT IDENTITY(1,1) PRIMARY KEY, _LOADED_AT DATETIME DEFAULT GETDATE()

5. TRUNCATE + SqlBulkCopy the rows into the SQL table

6. Log to RFC_PIPELINE_LOG table: (RFC_NAME, RUN_DATE, ROWS_LOADED, STATUS, ERROR_MSG, DURATION_SEC)

7. SQL connection string read from App.config key "SqlConnectionString"
   SAP config also from App.config keys: SAP_HOST, SAP_CLIENT, SAP_USER, SAP_PASS

8. Args: no args = use default params + yesterday's date. With args: positional date params.

Also generate App.config with pre-filled values:
- SAP_HOST = ${env.host}
- SAP_CLIENT = ${env.client}  
- SAP_USER = ${cfg.sap.user}
- SAP_PASS = ${cfg.sap.password}
- SqlConnectionString = Data Source=.;Initial Catalog=V2_RFC_DATALAKE;Integrated Security=True;

Return a JSON object with two keys: "programCs" (the full Program.cs content) and "appConfig" (the full App.config content). No markdown.`;

  const res = await axios.post('https://api.anthropic.com/v1/messages', {
    model: cfg.anthropic.model,
    max_tokens: cfg.anthropic.maxTokens,
    messages: [{ role: 'user', content: prompt }]
  }, {
    headers: { 'Content-Type': 'application/json', 'x-api-key': process.env.ANTHROPIC_API_KEY || '' }
  });

  const raw = res.data.content?.find(b => b.type === 'text')?.text || '';
  const cleaned = raw.replace(/```json\n?/g,'').replace(/```\n?/g,'').trim();
  try {
    return JSON.parse(cleaned);
  } catch {
    return { programCs: cleaned, appConfig: '' };
  }
}

module.exports = { generateController, generatePipeline };
