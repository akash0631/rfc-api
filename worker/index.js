/**
 * V2 Retail · RFC Pipeline Worker
 * Upload RFC .docx → Parse → Generate → Push GitHub → Live API
 */

const GITHUB_REPO      = 'akash0631/rfc-api';
const GITHUB_BRANCH    = 'master';
const DAB_APP_URL      = 'https://my-dab-app.azurewebsites.net';
const IIS_HOST         = 'https://sap-api.v2retail.net';  // sap-api.v2retail.net → CF Tunnel → .36:9292
const GH_WORKFLOW_ID   = '245504825';  // deploy-iis.yml (Build and Deploy to .36 IIS)
const sleep = ms => new Promise(r => setTimeout(r, ms));
const SAP_ENVS = {
  dev:        { fn: 'rfcConfigparameters',           host: '192.168.144.174', client: '210' },
  quality:    { fn: 'rfcConfigparametersquality',    host: '192.168.144.179', client: '600' },
  production: { fn: 'rfcConfigparametersproduction', host: '192.168.144.170', client: '600' },
};
const FOLDER_MAP = {
  Finance:'Controllers/Finance', GateEntry:'Controllers/GateEntry_LOT_Putway',
  Vendor:'Controllers/Vendor_SRM_Routing', HUCreation:'Controllers/HU_Creation',
  FabricPutway:'Controllers/FMS_FABRIC_PUTWAY', HRMS:'Controllers/HRMS',
  NSO:'Controllers/NSO', PaperlessPicklist:'Controllers/PaperlessPicklist',
  Sampling:'Controllers/Sampling', VehicleLoading:'Controllers/Vehicle_Loading',
};
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

// ─── DOCX Text Extractor (pure JS, no deps) ──────────────────────────────────
// Reads ZIP entries to find word/document.xml and strips XML tags
async function extractDocxText(arrayBuffer) {
  const bytes = new Uint8Array(arrayBuffer);
  const entries = await parseZipAll(bytes);
  let text = '';
  const docXml = entries['word/document.xml'];
  if (docXml) {
    const xml = new TextDecoder().decode(docXml);
    text = xml
      .replace(/<w:p[ >]/g, '\n<w:p ')
      .replace(/<\/w:p>/g, '\n')
      .replace(/<[^>]+>/g, ' ')
      .replace(/\s{2,}/g, ' ')
      .replace(/\n +/g, '\n')
      .trim();
  }
  const images = [];
  for (const [name, data] of Object.entries(entries)) {
    if (name.startsWith('word/media/') && (name.endsWith('.png')||name.endsWith('.jpg')||name.endsWith('.jpeg'))) {
      const b64 = btoa(String.fromCharCode(...new Uint8Array(data)));
      images.push({ b64, mime: name.endsWith('.png')?'image/png':'image/jpeg' });
    }
  }
  return { text, images };
}

async function parseZipAll(bytes) {
  const entries = {};
  // Find End of Central Directory
  let eocd = -1;
  for (let i = bytes.length - 22; i >= 0; i--) {
    if (bytes[i]===0x50 && bytes[i+1]===0x4b && bytes[i+2]===0x05 && bytes[i+3]===0x06) { eocd=i; break; }
  }
  if (eocd<0) throw new Error('ZIP: EOCD not found');
  const cdOffset = read32(bytes, eocd+16);
  const cdSize   = read32(bytes, eocd+12);
  let pos = cdOffset;
  while (pos < cdOffset + cdSize) {
    if (read32(bytes,pos) !== 0x02014b50) break;
    const compMethod = read16(bytes, pos+10);
    const compSize   = read32(bytes, pos+20);
    const uncompSize = read32(bytes, pos+24);
    const nameLen    = read16(bytes, pos+28);
    const extraLen   = read16(bytes, pos+30);
    const commentLen = read16(bytes, pos+32);
    const localOffset= read32(bytes, pos+42);
    const name = new TextDecoder().decode(bytes.slice(pos+46, pos+46+nameLen));
    pos += 46 + nameLen + extraLen + commentLen;
    // Extract document.xml AND media images
    if (name === 'word/document.xml' || name.startsWith('word/media/')) {
      // Read local file header
      const lhExtraLen = read16(bytes, localOffset+28);
      const lhNameLen  = read16(bytes, localOffset+26);
      const dataStart  = localOffset + 30 + lhNameLen + lhExtraLen;
      const compData   = bytes.slice(dataStart, dataStart + compSize);
      if (compMethod === 0) {
        entries[name] = compData;
      } else if (compMethod === 8) {
        try {
          const ds = new DecompressionStream('deflate-raw');
          const writer = ds.writable.getWriter();
          const reader = ds.readable.getReader();
          writer.write(compData);
          writer.close();
          const chunks = [];
          let totalLen = 0;
          while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            chunks.push(value); totalLen += value.length;
          }
          const result = new Uint8Array(totalLen);
          let off = 0;
          for (const c of chunks) { result.set(c, off); off += c.length; }
          entries[name] = result;
        } catch(e) { /* skip if decompression fails */ }
      }
    }
  }
  return entries;
}
function read16(b,o){return b[o]|(b[o+1]<<8);}
function read32(b,o){return (b[o]|(b[o+1]<<8)|(b[o+2]<<16)|(b[o+3]<<24))>>>0;}

// ─── GitHub helpers ───────────────────────────────────────────────────────────
async function ghGet(path, token) {
  const r = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/contents/${path}`,
    {headers:{Authorization:`token ${token}`,Accept:'application/vnd.github.v3+json','User-Agent':'V2-RFC-Pipeline'}});
  if (r.status===404) return {content:null,sha:null,exists:false};
  const d = await r.json();
  const content = d.content ? atob(d.content.replace(/\n/g,'')) : null;
  return {content, sha:d.sha, exists:true};
}
async function ghPut(path, content, sha, message, token) {
  const encoded = btoa(String.fromCharCode(...new Uint8Array(new TextEncoder().encode(content))));
  const body = {message, content:encoded, branch:GITHUB_BRANCH};
  if (sha) body.sha = sha;
  const r = await fetch(`https://api.github.com/repos/${GITHUB_REPO}/contents/${path}`,
    {method:'PUT', headers:{Authorization:`token ${token}`,Accept:'application/vnd.github.v3+json','Content-Type':'application/json','User-Agent':'V2-RFC-Pipeline'},
     body:JSON.stringify(body)});
  const d = await r.json();
  if (!r.ok) throw new Error(`GitHub PUT ${r.status}: ${d.message||JSON.stringify(d).slice(0,200)}`);
  return {commitSha: d.commit?.sha?.slice(0,7), commitUrl:`https://github.com/${GITHUB_REPO}/commit/${d.commit?.sha}`};
}

// ─── Claude API call ──────────────────────────────────────────────────────────
async function claude(prompt, apiKey, maxTokens=2500) {
  const r = await fetch('https://api.anthropic.com/v1/messages',{
    method:'POST',
    headers:{'Content-Type':'application/json','x-api-key':apiKey,'anthropic-version':'2023-06-01'},
    body:JSON.stringify({model:'claude-sonnet-4-20250514',max_tokens:maxTokens,
      messages:[{role:'user',content:prompt}]})
  });
  const d = await r.json();
  if (!r.ok) throw new Error(d.error?.message||'Claude API error');
  return d.content?.find(b=>b.type==='text')?.text||'';
}

// ─── Parse RFC spec from text ─────────────────────────────────────────────────
async function parseRfc(text, apiKey, filename='', images=[]) {
  const filenameHint = filename ? `\nHint: The filename is "${filename}" — use this to infer the RFC name if not explicit in the document.` : '';
  // Build message content — use images if text is short (image-based SAP docx)
  let msgContent;
  if (images && images.length > 0 && text.length < 200) {
    msgContent = [
      ...images.map(img => ({type:'image',source:{type:'base64',media_type:img.mime,data:img.b64}})),
      {type:'text',text:`You are parsing SAP Function Builder screenshots for V2 Retail.
Extract the RFC specification from these images and return ONLY valid JSON:
{
  "rfcName": "RFC function name visible in the screenshots e.g. ZPO_DD_UPD_RFC",
  "description": "one-line description of what this RFC does",
  "category": "one of: Finance,GateEntry,Vendor,HUCreation,FabricPutway,HRMS,NSO,PaperlessPicklist,Sampling,VehicleLoading",
  "importParams": [{"name":"PARAM_NAME","sapType":"SAP_TYPE","description":"what it is"}],
  "outputType": "table OR return_only",
  "outputTableName": "TABLE param name or null",
  "outputFields": [{"fieldName":"FIELD","sapType":"TYPE","length":"LENGTH"}],
  "suggestedSqlTable": "ET_RFCNAME"
}
${filenameHint}`}
    ];
  } else {
    msgContent = `You are parsing a SAP RFC specification document for V2 Retail.
Extract the following and return ONLY valid JSON (no markdown, no explanation):
{
  "rfcName": "RFC function name from the document e.g. ZVND_GATELOT2_PICKLIST_VAL_RFC",
  "description": "one-line description",
  "category": "one of: Finance,GateEntry,Vendor,HUCreation,FabricPutway,HRMS,NSO,PaperlessPicklist,Sampling,VehicleLoading",
  "importParams": [{"name":"PARAM","sapType":"TYPE","description":"what it is"}],
  "outputType": "table OR return_only",
  "outputTableName": "TABLE param name or null",
  "outputFields": [{"fieldName":"F","sapType":"T","length":"L"}],
  "suggestedSqlTable": "ET_RFCNAME (ET_ prefix, no _RFC suffix)"
}
RFC Document:
\${text.slice(0,5000)}\${filenameHint}`;
  }
  
  const r2 = await fetch('https://api.anthropic.com/v1/messages',{
    method:'POST',
    headers:{'Content-Type':'application/json','x-api-key':apiKey,'anthropic-version':'2023-06-01'},
    body:JSON.stringify({model:'claude-sonnet-4-20250514',max_tokens:800,
      messages:[{role:'user',content:msgContent}]})
  });
  const d2 = await r2.json();
  if (!r2.ok) throw new Error(d2.error?.message||'Claude API error');
  const raw = d2.content?.find(b=>b.type==='text')?.text||'';
  return JSON.parse(raw.replace(/```json\n?/g,'').replace(/```/g,'').trim());
}

// ─── Generate C# controller ───────────────────────────────────────────────────
async function genController(spec, sapEnv, apiKey) {
  const env = SAP_ENVS[sapEnv];
  const importBlock = spec.importParams?.map(p=>`- ${p.name} (SAP TYPE: ${p.sapType})`).join('\n')||'(none)';
  const outputDesc = spec.outputType==='table'
    ? `Returns TABLE "${spec.outputTableName}" — dynamic metadata loop, skip STRUCTURE/TABLE fields. Response: {Status,Message,Data:{${spec.outputTableName}:[rows]}}`
    : `Returns EX_RETURN only. Response: {Status,Message}`;
  const raw = await claude(`You are a senior C# .NET Web API developer for V2 Retail SAP RFC REST API.
Generate a COMPLETE, production-ready ASP.NET Web API controller (.NET 4.7.2).

RFC: ${spec.rfcName}
Description: ${spec.description}
SAP: ${env.host} / Client ${env.client}

IMPORT params:
${importBlock}

Output: ${outputDesc}

EXACT usings (copy verbatim):
${USINGS}

Namespace: Vendor_SRM_Routing_Application.Controllers.PaperlessPicklist
Class: ${spec.rfcName}Controller : BaseController
Route: [HttpPost] [Route("api/${spec.rfcName}")]
Request model class at bottom of file.
Error handling: RfcAbapException, CommunicationException, Exception → {Status:"E",Message:ex.Message}
Check EX_RETURN after invoke — if TYPE=="E" return error.

MANDATORY SAP CONNECTOR PATTERN — copy exactly, no variations:
  RfcConfigParameters rfcPar = BaseController.${env.fn}();
  RfcDestination dest = RfcDestinationManager.GetDestination(rfcPar);
  RfcRepository rfcrep = dest.Repository;
  IRfcFunction myfun = rfcrep.CreateFunction("${spec.rfcName}");
  // SetValue calls here
  myfun.Invoke(dest);
  IRfcStructure EX_RETURN = myfun.GetStructure("EX_RETURN");

NEVER use rfcConfigparameters.GetFunction() — that pattern does not exist.
For TABLE output use: IRfcTable tbl = myfun.GetTable("TABLE_NAME"); then .AsEnumerable().Select(...)

Return ONLY raw C#. No markdown.`, apiKey, 2500);
  return raw.replace(/```(?:csharp|cs)?\n?/g,'').replace(/```$/g,'').trim();
}

// ─── Push controller to GitHub ────────────────────────────────────────────────
async function pushController(spec, code, sapEnv, token) {
  const folder = FOLDER_MAP[spec.category]||'Controllers/NSO';
  const fp = `${folder}/${spec.rfcName}Controller.cs`;
  const {sha} = await ghGet(fp, token);
  return {
    ...(await ghPut(fp, code, sha, `Add ${spec.rfcName} controller [${sapEnv.toUpperCase()}] via RFC Portal`, token)),
    filePath: fp
  };
}

// ─── Register DAB entity ──────────────────────────────────────────────────────
async function registerDab(spec, token) {
  const tbl = spec.suggestedSqlTable||`ET_${spec.rfcName.replace(/_RFC$/,'')}`;
  const {content,sha} = await ghGet('dab-config.json', token);
  if (!content) throw new Error('dab-config.json not found');
  const cfg = JSON.parse(content);
  cfg.entities = cfg.entities||{};
  cfg.entities[tbl] = {
    source:{object:`dbo.${tbl}`,type:'table','key-fields':['ID']},
    permissions:[{role:'anonymous',actions:[{action:'read'}]}],
    rest:{enabled:true,path:`/api/${tbl}`},
    graphql:{enabled:false}
  };
  const r = await ghPut('dab-config.json', JSON.stringify(cfg,null,2), sha,
    `Register ${tbl} in DAB config for ${spec.rfcName}`, token);
  return {...r, tbl, endpoint:`${DAB_APP_URL}/api/${tbl}`};
}

// ─── Update Swagger HTML ──────────────────────────────────────────────────────
async function updateSwagger(spec, sapEnv, token) {
  const env = SAP_ENVS[sapEnv];
  const envColors={dev:'#16a34a',quality:'#d97706',production:'#dc2626'};
  const envBg={dev:'#f0fdf4',quality:'#fffbeb',production:'#fef2f2'};
  const tbl = spec.suggestedSqlTable||`ET_${spec.rfcName.replace(/_RFC$/,'')}`;
  const pj = JSON.stringify(Object.fromEntries((spec.importParams||[]).map(p=>[p.name,`[${p.sapType}]`])),null,2);
  const card = `
  <!-- AUTO:${spec.rfcName}:${new Date().toISOString().slice(0,10)} -->
  <div style="border:1px solid #e4e8f0;border-radius:10px;padding:16px;margin-bottom:12px;background:#fff;">
    <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px;flex-wrap:wrap;">
      <span style="background:#4361ee;color:#fff;border-radius:5px;padding:3px 10px;font-size:11px;font-weight:700;font-family:monospace;">POST</span>
      <code style="font-size:13px;font-weight:600;">/api/${spec.rfcName}</code>
      <span style="background:${envBg[sapEnv]};border:1px solid ${envColors[sapEnv]};color:${envColors[sapEnv]};padding:2px 9px;border-radius:5px;font-size:11px;font-family:monospace;">${sapEnv.toUpperCase()} · ${env.host}</span>
    </div>
    <p style="font-size:12px;color:#475569;margin-bottom:10px;">${spec.description}</p>
    <pre style="background:#13141f;color:#9aa5d4;padding:12px;border-radius:7px;font-size:11px;overflow-x:auto;">${pj}</pre>
    <div style="margin-top:8px;font-size:11px;color:#64748b;font-family:monospace;">
      ↓ Data Lake: <a href="${DAB_APP_URL}/api/${tbl}" style="color:#4361ee;">${DAB_APP_URL}/api/${tbl}</a>
    </div>
  </div>`;
  const {content,sha} = await ghGet('v2_sap_api_explorer.html', token);
  let html = content
    ? (content.includes('<!-- END ENDPOINTS -->')
        ? content.replace('<!-- END ENDPOINTS -->', card+'\n  <!-- END ENDPOINTS -->')
        : content.replace('</body>', card+'\n</body>'))
    : `<!DOCTYPE html><html><head><meta charset="utf-8"><title>V2 RFC API</title></head><body style="font-family:sans-serif;max-width:900px;margin:40px auto;padding:0 20px;">\n<h1 style="font-size:20px;margin-bottom:20px;">V2 Retail · RFC API Endpoints</h1>\n${card}\n  <!-- END ENDPOINTS -->\n</body></html>`;
  return ghPut('v2_sap_api_explorer.html', html, sha, `Swagger: add ${spec.rfcName}`, token);
}

// ─── Full pipeline ────────────────────────────────────────────────────────────
async function runPipeline(text, sapEnv, jobId, env, filename='', images=[]) {
  const apiKey  = env.ANTHROPIC_API_KEY;
  const ghToken = env.GITHUB_TOKEN;
  const kv      = env.RFC_JOBS;
  const RELAY   = 'https://sap-api.v2retail.net';
  const DAB_URL = 'https://my-dab-app.azurewebsites.net';

  const TOTAL_STEPS = 9; // parse, controller, github, deploy, sql_create, data_sync, dab, dab_verify, swagger
  const log = async (step, status, detail='') => {
    const job = JSON.parse(await kv.get(jobId)||'{}');
    job.steps = job.steps||[];
    const existing = job.steps.find(s=>s.step===step);
    if (existing) { existing.status=status; existing.detail=detail; }
    else job.steps.push({step, status, detail});
    // Only mark complete when we have all steps finished (done at the very end of pipeline)
    // Never auto-complete mid-pipeline
    await kv.put(jobId, JSON.stringify(job), {expirationTtl:86400});
  };

  // ── STEP 1: Parse RFC document ──────────────────────────────────────────
  await log('parse','running','Extracting RFC spec with Claude AI...');
  let spec;
  try { spec = await parseRfc(text, apiKey, filename, images); }
  catch(e) { await log('parse','error',e.message); return; }
  await log('parse','done',`${spec.rfcName} · ${spec.category}`);

  // ── STEP 2: Generate C# controller ─────────────────────────────────────
  await log('controller','running','Generating ASP.NET C# controller...');
  let ctrlCode;
  try { ctrlCode = await genController(spec, sapEnv, apiKey); }
  catch(e) { await log('controller','error',e.message); return; }
  await log('controller','done',`${ctrlCode.split('\n').length} lines generated`);

  // ── STEP 3: Push to GitHub ──────────────────────────────────────────────
  await log('github','running','Pushing controller to GitHub...');
  let ctrlResult;
  try { ctrlResult = await pushController(spec, ctrlCode, sapEnv, ghToken); }
  catch(e) { await log('github','error',e.message); return; }
  await log('github','done',`${ctrlResult.filePath} (${ctrlResult.commitSha.slice(0,8)})`);

  // ── STEP 4: Trigger IIS Deploy (non-blocking — GitHub push already triggers it) ──
  // The push to Controllers/** in step 3 already auto-triggers deploy-iis.yml.
  // We dispatch separately as backup and record the run URL, then continue immediately.
  await log('deploy','running','Dispatching IIS build + deploy...');
  try {
    // Dispatch the workflow
    await fetch(
      `https://api.github.com/repos/${GITHUB_REPO}/actions/workflows/${GH_WORKFLOW_ID}/dispatches`,
      { method:'POST',
        headers:{ Authorization:`token ${ghToken}`, Accept:'application/vnd.github+json',
          'Content-Type':'application/json', 'User-Agent':'V2-RFC-Pipeline' },
        body: JSON.stringify({ref: GITHUB_BRANCH}) }
    );
    await sleep(5000);
    // Find the new run (don't block waiting for it to finish)
    const runsRes = await fetch(
      `https://api.github.com/repos/${GITHUB_REPO}/actions/runs?per_page=3&event=workflow_dispatch`,
      { headers:{ Authorization:`token ${ghToken}`, 'User-Agent':'V2-RFC-Pipeline' } }
    );
    const runs = await runsRes.json();
    const fresh = runs.workflow_runs?.[0];
    const runUrl = fresh ? `https://github.com/${GITHUB_REPO}/actions/runs/${fresh.id}` : '';
    await log('deploy','done',`Deploy triggered ✓ sap-api.v2retail.net/api/${spec.rfcName}/Post`);
  } catch(e) {
    // Non-fatal — GitHub push already triggers deploy via path filter
    await log('deploy','done',`Triggered via GitHub push (path filter) · sap-api.v2retail.net`);
  }


  // ── Final: write job summary ────────────────────────────────────────────
  const sqlTable = spec.suggestedSqlTable || `ET_${spec.rfcName.replace(/_RFC$/,'')}`;
  const finalJob = JSON.parse(await kv.get(jobId)||'{}');
  finalJob.status    = 'complete';
  finalJob.rfcName   = spec.rfcName;
  finalJob.rfcApi    = `https://sap-api.v2retail.net/api/${spec.rfcName}/Post`;
  finalJob.dataLake  = `https://my-dab-app.azurewebsites.net/api/${sqlTable}`;
  finalJob.sqlTable  = sqlTable;
  finalJob.swagger   = `https://v2-rfc-portal.pages.dev/rfc_hub`;
  finalJob.commit    = ctrlResult.commitUrl;
  await kv.put(jobId, JSON.stringify(finalJob), {expirationTtl:86400});
}




// ─── Manage Data Lake Columns ─────────────────────────────────────────────────
async function manageColumns(tableName, operations, token) {
  // operations: [{action:'ADD'|'REMOVE', column:'COL_NAME', sqlType:'NVARCHAR(255)'}]
  const tbl = tableName.trim().toUpperCase();

  // Build ALTER TABLE SQL
  const sqlLines = operations.map(op => {
    if (op.action === 'ADD') {
      return `ALTER TABLE dbo.${tbl} ADD ${op.column.trim().toUpperCase()} ${op.sqlType||'NVARCHAR(255)'} NULL;`;
    } else {
      return `ALTER TABLE dbo.${tbl} DROP COLUMN ${op.column.trim().toUpperCase()};`;
    }
  });
  const ts = new Date().toISOString().replace(/[-:T]/g,'').slice(0,14);
  const actionSummary = operations.map(o=>`${o.action} ${o.column.trim().toUpperCase()}`).join(', ');
  const sql = `-- V2 Retail Data Lake · Column Migration\n-- Table : dbo.${tbl}\n-- Change: ${actionSummary}\n-- Date  : ${new Date().toISOString().slice(0,10)}\n-- Run this on the Azure SQL Server connected via VPN\n\nUSE [V2_DataLake];\nGO\n\n${sqlLines.join('\n')}\nGO\n`;

  // Push SQL migration file to GitHub
  const migPath = `DataPipelines/${tbl}/migrations/${ts}_columns.sql`;
  const r = await ghPut(migPath, sql, null, `[columns] ${actionSummary} on ${tbl}`, token);

  // Update dab-config.json: add/remove field mappings
  const {content, sha} = await ghGet('dab-config.json', token);
  let dabUpdated = false;
  if (content) {
    const cfg = JSON.parse(content);
    cfg.entities = cfg.entities || {};
    // Try to find the entity (exact or case-insensitive match)
    let entityKey = Object.keys(cfg.entities).find(k => k.toUpperCase() === tbl) || tbl;
    if (!cfg.entities[entityKey]) {
      cfg.entities[entityKey] = {
        source:{object:`dbo.${tbl}`,type:'table','key-fields':['ID']},
        permissions:[{role:'anonymous',actions:[{action:'read'}]}],
        rest:{enabled:true,path:`/api/${tbl}`},graphql:{enabled:false}
      };
    }
    const ent = cfg.entities[entityKey];
    if (!ent.mappings) ent.mappings = {};
    for (const op of operations) {
      const col = op.column.trim().toUpperCase();
      if (op.action === 'ADD') {
        ent.mappings[col] = col;
      } else {
        delete ent.mappings[col];
      }
    }
    if (Object.keys(ent.mappings).length === 0) delete ent.mappings;
    await ghPut('dab-config.json', JSON.stringify(cfg,null,2), sha,
      `[dab] update field mappings for ${tbl} · ${actionSummary}`, token);
    dabUpdated = true;
  }

  return {sql, migPath, commitUrl: r.commitUrl, commitSha: r.commitSha, dabUpdated};
}


// ─── Data Lake Sync Engine ────────────────────────────────────────────────────
const IIS_BASE = 'http://v2retail.net:9005';
const DAB_BASE = 'https://my-dab-app.azurewebsites.net';

function extractRows(data) {
  if (Array.isArray(data)) return data;
  // SAP controllers return {Status, Message, Data:{TABLE:[rows]}} or {Status, Data:[rows]}
  const d = data.Data || data.data;
  if (!d) return [];
  if (Array.isArray(d)) return d;
  // Find first array value in Data object (the main table)
  for (const key of Object.keys(d)) {
    if (Array.isArray(d[key]) && key !== 'EX_RETURN' && key !== 'ES_RETURN') return d[key];
  }
  return [];
}

async function syncOne(job, env) {
  const { rfcName, tableName, params = {} } = job;
  const ts = new Date().toISOString();
  const result = { rfcName, tableName, startedAt: ts };

  try {
    // 1. Call IIS → SAP
    const iisRes = await fetch(`${IIS_BASE}/api/${rfcName}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams(params).toString(),
    });
    if (!iisRes.ok) throw new Error(`IIS ${iisRes.status}: ${await iisRes.text()}`);
    const sapData = await iisRes.json();

    // Check SAP-level errors
    if (sapData.Status === 'E') throw new Error(`SAP error: ${sapData.Message || JSON.stringify(sapData)}`);

    const rows = extractRows(sapData);
    result.rowsFetched = rows.length;

    // 2. Upsert into Azure SQL via DAB REST API
    let inserted = 0, failed = 0, failedRows = [];
    for (const row of rows) {
      const payload = { ...row, _SYNC_AT: ts, _RFC: rfcName };
      const dabRes = await fetch(`${DAB_BASE}/api/${tableName}`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (dabRes.ok) {
        inserted++;
      } else {
        failed++;
        if (failedRows.length < 3) {
          const err = await dabRes.text();
          failedRows.push({ status: dabRes.status, error: err.slice(0, 200) });
        }
      }
    }

    result.inserted = inserted;
    result.failed = failed;
    result.failedSamples = failedRows;
    result.status = failed === 0 ? 'ok' : rows.length === 0 ? 'empty' : 'partial';
    result.finishedAt = new Date().toISOString();

  } catch(e) {
    result.status = 'error';
    result.error = e.message;
    result.finishedAt = new Date().toISOString();
  }

  // Store result (7-day TTL)
  await env.RFC_JOBS.put(`sync_result:${rfcName}`,
    JSON.stringify(result), { expirationTtl: 86400 * 7 });
  return result;
}

async function runAllSyncs(env) {
  const list = await env.RFC_JOBS.list({ prefix: 'sync_job:' });
  const results = [];
  for (const key of list.keys) {
    const job = JSON.parse(await env.RFC_JOBS.get(key.name) || '{}');
    if (job.enabled === false) continue;
    const r = await syncOne(job, env);
    results.push(r);
  }
  return results;
}

// ─── Sync Dashboard HTML ──────────────────────────────────────────────────────
const SYNC_HTML = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>V2 Retail · Data Lake Sync</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
:root{--bg:#f5f7fc;--white:#fff;--border:#e4e8f0;--accent:#4361ee;--al:#eef1fd;
  --green:#16a34a;--gl:#f0fdf4;--red:#dc2626;--rl:#fef2f2;--orange:#d97706;--ol:#fffbeb;
  --text:#0f172a;--sub:#475569;--muted:#94a3b8;
  --sans:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;
  --mono:Consolas,'Courier New',monospace}
body{background:var(--bg);font-family:var(--sans);min-height:100vh}
.top{background:#0f172a;height:52px;display:flex;align-items:center;justify-content:space-between;
  padding:0 24px;position:sticky;top:0;z-index:50;box-shadow:0 2px 12px rgba(0,0,0,.3)}
.brand{display:flex;align-items:center;gap:8px}
.bdot{width:7px;height:7px;border-radius:50%;background:#34d399;animation:blink 2s infinite}
@keyframes blink{0%,100%{opacity:1}50%{opacity:.25}}
.bname{font-size:14px;font-weight:800;color:#fff}.bname span{color:#4361ee}
.btag{font-size:10px;background:rgba(67,97,238,.2);border:1px solid rgba(67,97,238,.4);
  color:#818cf8;padding:2px 8px;border-radius:999px;font-family:monospace}
.nav a{color:#94a3b8;text-decoration:none;font-size:12px;font-weight:600;
  padding:5px 11px;border-radius:6px;transition:.15s}
.nav a:hover{color:#fff;background:rgba(255,255,255,.08)}
.app{max-width:860px;margin:0 auto;padding:32px 16px 80px}
.page-title{font-size:22px;font-weight:800;color:var(--text);margin-bottom:4px}
.page-sub{font-size:12px;color:var(--muted);font-family:var(--mono);margin-bottom:24px}
.card{background:var(--white);border:1px solid var(--border);border-radius:14px;
  padding:22px;box-shadow:0 1px 3px rgba(0,0,0,.04);margin-bottom:14px}
.ct{font-size:9px;font-family:var(--mono);font-weight:600;color:var(--muted);
  letter-spacing:2px;text-transform:uppercase;margin-bottom:16px;
  display:flex;align-items:center;gap:8px}
.ct::after{content:'';flex:1;height:1px;background:var(--border)}
.form-grid{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:12px}
.form-grid.three{grid-template-columns:1fr 1fr 1fr}
.field label{font-size:10.5px;font-weight:700;letter-spacing:.8px;
  text-transform:uppercase;color:var(--muted);display:block;margin-bottom:5px}
.field input,.field textarea{width:100%;background:var(--bg);border:1.5px solid var(--border);
  border-radius:8px;padding:9px 12px;color:var(--text);font-size:12.5px;
  font-family:var(--mono);outline:none;transition:.15s;resize:vertical}
.field input:focus,.field textarea:focus{border-color:var(--accent);background:var(--white);
  box-shadow:0 0 0 3px rgba(67,97,238,.08)}
.field input::placeholder{color:var(--muted)}
.hint{font-size:10.5px;color:var(--muted);margin-top:4px;font-family:var(--mono)}
.btn{padding:10px 18px;border:none;border-radius:8px;background:var(--accent);
  color:#fff;font-size:13px;font-weight:700;cursor:pointer;transition:.15s;
  display:inline-flex;align-items:center;gap:6px}
.btn:hover{background:#3451d1}
.btn:disabled{opacity:.4;cursor:not-allowed}
.btn-sm{padding:5px 12px;font-size:11.5px;border-radius:6px}
.btn-ghost{background:none;border:1.5px solid var(--border);color:var(--sub)}
.btn-ghost:hover{border-color:var(--accent);color:var(--accent);background:var(--al)}
.btn-danger{background:var(--rl);border:1.5px solid #fca5a5;color:var(--red)}
.btn-danger:hover{background:#fee2e2}
.btn-green{background:var(--green)}
.btn-green:hover{background:#15803d}
.jobs-table{width:100%;border-collapse:collapse}
.jobs-table th{padding:8px 12px;font-size:9.5px;font-weight:700;letter-spacing:1.2px;
  text-transform:uppercase;color:var(--muted);text-align:left;
  border-bottom:1px solid var(--border);background:#fafbfd}
.jobs-table td{padding:10px 12px;font-size:12.5px;border-bottom:1px solid var(--border);
  color:var(--sub);vertical-align:middle}
.jobs-table tr:last-child td{border-bottom:none}
.jobs-table tr:hover td{background:#fafbff}
.badge{display:inline-flex;align-items:center;gap:4px;font-size:10px;
  font-weight:700;padding:2px 8px;border-radius:4px;font-family:var(--mono)}
.badge-ok{background:var(--gl);color:var(--green);border:1px solid #86efac}
.badge-error{background:var(--rl);color:var(--red);border:1px solid #fca5a5}
.badge-partial{background:var(--ol);color:var(--orange);border:1px solid #fcd34d}
.badge-empty{background:var(--al);color:var(--accent);border:1px solid #c7d2fe}
.badge-never{background:#f1f5f9;color:var(--muted);border:1px solid var(--border)}
.rfcname{color:var(--accent);font-family:var(--mono);font-weight:600}
.mono{font-family:var(--mono);font-size:11.5px}
.actions{display:flex;gap:6px}
.spin{display:inline-block;width:12px;height:12px;border:2px solid currentColor;
  border-top-color:transparent;border-radius:50%;animation:rot .6s linear infinite}
@keyframes rot{to{transform:rotate(360deg)}}
.run-all-bar{display:flex;align-items:center;justify-content:space-between;margin-bottom:16px}
.schedule-note{font-size:11.5px;color:var(--muted);font-family:var(--mono)}
.toast{position:fixed;bottom:24px;right:24px;background:#0f172a;color:#fff;
  padding:10px 18px;border-radius:8px;font-size:13px;font-weight:600;
  box-shadow:0 4px 20px rgba(0,0,0,.3);z-index:999;display:none}
.params-preview{font-size:10.5px;font-family:var(--mono);color:var(--muted);
  max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.empty-state{text-align:center;padding:40px;color:var(--muted)}
.empty-icon{font-size:36px;margin-bottom:10px}
.stat-row{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:16px}
.stat{background:var(--bg);border:1px solid var(--border);border-radius:10px;
  padding:12px 16px;text-align:center}
.stat-num{font-size:24px;font-weight:800;color:var(--accent);font-family:var(--mono)}
.stat-lbl{font-size:10.5px;color:var(--muted);margin-top:2px}
</style>
</head>
<body>
<div class="top">
  <div class="brand"><div class="bdot"></div>
    <div class="bname">V2 Retail · <span>Data Lake Sync</span></div>
    <div class="btag">CRON 02:00 IST</div>
  </div>
  <div class="nav" style="display:flex;gap:4px">
    <a href="/">⚡ Deploy RFC</a>
    <a href="/explore">🔍 Explorer</a>
    <a href="/sync" style="color:#fff;background:rgba(67,97,238,.25);border:1px solid rgba(67,97,238,.4);padding:5px 11px;border-radius:6px">🔄 Sync</a>
  </div>
</div>

<div class="app">
  <div class="page-title">Data Lake Sync</div>
  <div class="page-sub">v2retail.net:9005 → Azure SQL (via DAB) · auto-runs daily 02:00 IST</div>

  <div class="stat-row">
    <div class="stat"><div class="stat-num" id="st-total">–</div><div class="stat-lbl">Registered RFCs</div></div>
    <div class="stat"><div class="stat-num" id="st-ok" style="color:#16a34a">–</div><div class="stat-lbl">Last Sync OK</div></div>
    <div class="stat"><div class="stat-num" id="st-err" style="color:#dc2626">–</div><div class="stat-lbl">Last Sync Errors</div></div>
  </div>

  <!-- Register New RFC -->
  <div class="card">
    <div class="ct">Register RFC for Sync</div>
    <div class="form-grid three">
      <div class="field">
        <label>RFC / Endpoint Name</label>
        <input id="f-rfc" placeholder="ZADVANCE_PAYMENT_RFC" oninput="autoTable()">
        <div class="hint">POST /api/{name} on IIS</div>
      </div>
      <div class="field">
        <label>DAB Table Name</label>
        <input id="f-table" placeholder="ET_ZADVANCE_PAYMENT">
        <div class="hint">Azure SQL table via DAB</div>
      </div>
      <div class="field">
        <label>Cron Label (optional)</label>
        <input id="f-label" placeholder="Advance Payment Daily">
      </div>
    </div>
    <div class="field" style="margin-bottom:12px">
      <label>Request Params (JSON) — leave {} for full pull</label>
      <textarea id="f-params" rows="3" placeholder='{"I_COMPANY_CODE":"1000"}'>{}</textarea>
      <div class="hint">These params are POSTed to the IIS endpoint each sync run</div>
    </div>
    <button class="btn" onclick="registerJob()">＋ Register RFC</button>
  </div>

  <!-- Job List -->
  <div class="card">
    <div class="run-all-bar">
      <div class="ct" style="margin:0;flex:1">Registered Sync Jobs</div>
      <button class="btn btn-sm btn-green" id="runAllBtn" onclick="runAll()" style="margin-left:16px">
        ▶ Run All Now
      </button>
    </div>
    <div id="jobs-container">
      <div class="empty-state"><div class="empty-icon">🔄</div><div>Loading jobs…</div></div>
    </div>
    <div class="schedule-note" style="margin-top:12px">
      ⏰ Auto-runs daily at 02:00 IST via Cloudflare CRON · Next run pulls fresh data from v2retail.net into Azure SQL
    </div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
let jobs = [];

async function load() {
  const r = await fetch('/sync/jobs');
  jobs = await r.json();
  renderJobs();
  updateStats();
}

function updateStats() {
  document.getElementById('st-total').textContent = jobs.length;
  document.getElementById('st-ok').textContent = jobs.filter(j=>j.result?.status==='ok').length;
  document.getElementById('st-err').textContent = jobs.filter(j=>j.result?.status==='error').length;
}

function renderJobs() {
  const el = document.getElementById('jobs-container');
  if (!jobs.length) {
    el.innerHTML = '<div class="empty-state"><div class="empty-icon">📭</div><div>No sync jobs yet — register an RFC above</div></div>';
    return;
  }
  el.innerHTML = '<table class="jobs-table"><thead><tr>'
    + '<th>RFC Name</th><th>DAB Table</th><th>Params</th><th>Last Sync</th><th>Rows</th><th>Status</th><th></th>'
    + '</tr></thead><tbody>'
    + jobs.map(j => {
      const r = j.result || {};
      const statusBadge = r.status
        ? \`<span class="badge badge-\${r.status}">\${r.status.toUpperCase()}</span>\`
        : '<span class="badge badge-never">NEVER</span>';
      const lastSync = r.finishedAt
        ? new Date(r.finishedAt).toLocaleString('en-IN',{dateStyle:'short',timeStyle:'short'})
        : '–';
      const rows = r.rowsFetched !== undefined ? r.rowsFetched : '–';
      const paramsStr = Object.keys(j.params||{}).length
        ? JSON.stringify(j.params).slice(0,40)+'…' : '(full pull)';
      return \`<tr>
        <td class="rfcname">\${j.rfcName}</td>
        <td class="mono">\${j.tableName}</td>
        <td><div class="params-preview" title='\${JSON.stringify(j.params||{})}'>\${paramsStr}</div></td>
        <td class="mono" style="font-size:11px">\${lastSync}</td>
        <td class="mono">\${rows}</td>
        <td>\${statusBadge}\${r.failed ? \` <span style="font-size:10px;color:#dc2626">(\${r.failed} err)</span>\`:''}
          \${r.error ? \`<div style="font-size:10px;color:#dc2626;font-family:monospace;margin-top:3px">\${r.error.slice(0,80)}</div>\`:''}</td>
        <td><div class="actions">
          <button class="btn btn-sm btn-ghost" onclick="runOne('\${j.rfcName}', this)">▶ Run</button>
          <button class="btn btn-sm btn-danger" onclick="deleteJob('\${j.rfcName}')">✕</button>
        </div></td>
      </tr>\`;
    }).join('')
    + '</tbody></table>';
}

function autoTable() {
  const rfc = document.getElementById('f-rfc').value.trim();
  if (!rfc) return;
  const tbl = 'ET_' + rfc.replace(/_RFC$/i,'').replace(/^Z/,'');
  document.getElementById('f-table').value = tbl;
}

async function registerJob() {
  const rfcName = document.getElementById('f-rfc').value.trim();
  const tableName = document.getElementById('f-table').value.trim();
  const label = document.getElementById('f-label').value.trim();
  let params = {};
  try { params = JSON.parse(document.getElementById('f-params').value||'{}'); }
  catch(e) { toast('Invalid JSON params'); return; }
  if (!rfcName||!tableName) { toast('RFC name and table required'); return; }
  const r = await fetch('/sync/register', {method:'POST',
    headers:{'Content-Type':'application/json'},
    body:JSON.stringify({rfcName,tableName,label,params})});
  if (r.ok) {
    toast('✅ Registered '+rfcName);
    document.getElementById('f-rfc').value='';
    document.getElementById('f-table').value='';
    document.getElementById('f-label').value='';
    document.getElementById('f-params').value='{}';
    await load();
  } else { toast('Error: '+(await r.text())); }
}

async function runOne(rfcName, btn) {
  btn.disabled=true; btn.innerHTML='<span class="spin"></span>';
  const r = await fetch('/sync/run/'+rfcName, {method:'POST'});
  const d = await r.json();
  toast(d.status==='ok' ? '✅ '+rfcName+' — '+d.rowsFetched+' rows' : '⚠️ '+rfcName+': '+(d.error||d.status));
  await load();
  btn.disabled=false; btn.textContent='▶ Run';
}

async function runAll() {
  const btn = document.getElementById('runAllBtn');
  btn.disabled=true; btn.innerHTML='<span class="spin"></span> Running…';
  const r = await fetch('/sync/run-all', {method:'POST'});
  const results = await r.json();
  const ok = results.filter(x=>x.status==='ok').length;
  toast(\`✅ \${ok}/\${results.length} syncs completed\`);
  await load();
  btn.disabled=false; btn.textContent='▶ Run All Now';
}

async function deleteJob(rfcName) {
  if (!confirm('Remove '+rfcName+' from sync schedule?')) return;
  await fetch('/sync/delete/'+rfcName, {method:'DELETE'});
  toast('Removed '+rfcName);
  await load();
}

function toast(msg) {
  const el = document.getElementById('toast');
  el.textContent = msg; el.style.display='block';
  setTimeout(()=>el.style.display='none', 3500);
}

load();
</script>
</body>
</html>`;

// ─── HTML Upload UI ───────────────────────────────────────────────────────────
const HTML = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>V2 Retail · RFC Pipeline</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
:root{--bg:#f5f7fc;--white:#fff;--border:#e4e8f0;--accent:#4361ee;--al:#eef1fd;--green:#16a34a;--gl:#f0fdf4;--red:#dc2626;--text:#0f172a;--sub:#475569;--muted:#94a3b8;--sans:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,system-ui,sans-serif;--mono:Consolas,'Courier New',monospace}
body{background:var(--bg);font-family:var(--sans);min-height:100vh;display:flex;flex-direction:column}
.top{background:#0f172a;height:52px;display:flex;align-items:center;justify-content:space-between;padding:0 24px;position:sticky;top:0;z-index:50;box-shadow:0 2px 12px rgba(0,0,0,.3)}
.brand{display:flex;align-items:center;gap:8px}
.bdot{width:7px;height:7px;border-radius:50%;background:#34d399;animation:blink 2s infinite}
@keyframes blink{0%,100%{opacity:1}50%{opacity:.25}}
.bname{font-size:14px;font-weight:800;color:#fff}.bname span{color:#4361ee}
.btag{font-size:10px;background:rgba(67,97,238,.2);border:1px solid rgba(67,97,238,.4);color:#818cf8;padding:2px 8px;border-radius:999px;font-family:monospace}
.nav a{color:#94a3b8;text-decoration:none;font-size:12px;font-weight:600;padding:5px 11px;border-radius:6px;transition:.15s}
.nav a:hover{color:#fff;background:rgba(255,255,255,.08)}
.app{max-width:680px;margin:0 auto;padding:36px 16px 80px;width:100%}
.hdr{text-align:center;margin-bottom:32px}
.pill{display:inline-flex;align-items:center;gap:6px;background:var(--al);border:1px solid #c7d2fe;border-radius:999px;padding:3px 14px 3px 8px;font-size:10px;font-family:var(--mono);color:var(--accent);margin-bottom:14px}
.dot{width:6px;height:6px;border-radius:50%;background:var(--green);animation:blink 2s infinite}
.hdr h1{font-size:28px;font-weight:800;letter-spacing:-.5px;margin-bottom:8px}
.hdr h1 em{font-style:normal;color:var(--accent)}
.hdr p{font-size:12px;color:var(--muted);font-family:var(--mono)}
.flow{display:flex;align-items:center;justify-content:center;gap:0;margin:24px 0;flex-wrap:wrap;gap:4px}
.fl{background:var(--white);border:1px solid var(--border);border-radius:8px;padding:7px 12px;font-size:11px;font-weight:600;color:var(--sub);font-family:var(--mono)}
.arr{color:var(--muted);font-size:14px;margin:0 2px}
.card{background:var(--white);border:1px solid var(--border);border-radius:14px;padding:22px;box-shadow:0 1px 3px rgba(0,0,0,.04);margin-bottom:14px}
.ct{font-size:9px;font-family:var(--mono);font-weight:600;color:var(--muted);letter-spacing:2px;text-transform:uppercase;margin-bottom:16px;display:flex;align-items:center;gap:8px}
.ct::after{content:'';flex:1;height:1px;background:var(--border)}
.drop{border:2px dashed var(--border);border-radius:10px;padding:40px 20px;text-align:center;cursor:pointer;transition:.2s;position:relative;background:var(--bg)}
.drop:hover,.drop.over{border-color:var(--accent);background:var(--al)}
.drop input{position:absolute;inset:0;opacity:0;cursor:pointer;width:100%;height:100%}
.drop-icon{font-size:36px;margin-bottom:10px}
.drop-title{font-size:15px;font-weight:700;margin-bottom:4px}
.drop-sub{font-size:11px;color:var(--muted);font-family:var(--mono)}
.file-sel{background:var(--gl);border:1px solid #86efac;border-radius:8px;padding:10px 14px;display:flex;align-items:center;gap:10px;font-size:12.5px;font-weight:600;color:var(--green);margin-top:10px}
.env-g{display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-top:10px}
.env-c{border:1.5px solid var(--border);border-radius:9px;padding:9px 11px;cursor:pointer;background:var(--bg);transition:.15s}
.env-label{font-size:13px;font-weight:700;margin-bottom:2px}
.env-sub{font-size:9.5px;font-family:var(--mono);color:var(--muted)}
.env-c.sel{border-color:transparent;box-shadow:0 0 0 2px var(--accent);background:var(--al)}
.env-c.sel .env-label{color:var(--accent)}
.btn{width:100%;padding:13px;border:none;border-radius:10px;background:var(--accent);color:#fff;font-family:var(--sans);font-size:14px;font-weight:700;cursor:pointer;box-shadow:0 3px 10px #4361ee33;transition:.15s;display:flex;align-items:center;justify-content:center;gap:8px;margin-top:6px}
.btn:hover{background:#3451d1;box-shadow:0 4px 14px #4361ee44}
.btn:disabled{opacity:.4;cursor:not-allowed;box-shadow:none}
.steps{display:flex;flex-direction:column;gap:7px}
.step{display:flex;align-items:flex-start;gap:12px;padding:12px 14px;border:1.5px solid var(--border);border-radius:10px;font-size:13px;font-weight:600;color:var(--muted);transition:.3s}
.step.run{border-color:#bfdbfe;background:#eff6ff;color:var(--accent)}
.step.done{border-color:#86efac;background:var(--gl);color:var(--green)}
.step.error{border-color:#fca5a5;background:#fef2f2;color:var(--red)}
.step-icon{width:22px;flex-shrink:0;display:grid;place-items:center;font-size:15px}
.step-detail{font-size:10.5px;font-family:var(--mono);margin-top:3px;opacity:.8;font-weight:400;word-break:break-all}
.spin{display:inline-block;width:14px;height:14px;border:2px solid currentColor;border-top-color:transparent;border-radius:50%;animation:rot .6s linear infinite;flex-shrink:0}
@keyframes rot{to{transform:rotate(360deg)}}
.result{text-align:center;padding:28px}
.result-icon{font-size:48px;margin-bottom:14px}
.result h2{font-size:20px;font-weight:800;color:var(--green);margin-bottom:6px}
.result p{font-size:11.5px;color:var(--muted);font-family:var(--mono);margin-bottom:20px}
.chips{display:flex;flex-wrap:wrap;gap:8px;justify-content:center;margin-bottom:20px}
.chip{display:inline-flex;align-items:center;gap:6px;border-radius:999px;padding:6px 14px;font-family:var(--mono);font-size:11px;text-decoration:none;font-weight:600}
.chip.g{background:var(--gl);border:1px solid #86efac;color:var(--green)}
.chip.b{background:var(--al);border:1px solid #c7d2fe;color:var(--accent)}
.ep-box{background:var(--bg);border:1px solid var(--border);border-radius:8px;padding:10px 16px;font-family:var(--mono);font-size:12px;color:var(--text);margin-bottom:20px;text-align:left;line-height:1.8}
.ep-box span{color:var(--muted);font-size:10px;display:block}
.btn-reset{background:none;border:1.5px solid var(--border);border-radius:9px;padding:10px 22px;font-family:var(--sans);font-size:13px;font-weight:600;color:var(--sub);cursor:pointer;transition:.15s}
.btn-reset:hover{border-color:#4361ee;color:#4361ee}
.tab-link{color:#94a3b8;text-decoration:none;font-size:12px;font-weight:600;padding:5px 11px;border-radius:6px;transition:.15s}
.tab-link:hover{color:#fff;background:rgba(255,255,255,.08)}
.tab-link.active{color:#fff;background:rgba(67,97,238,.25);border:1px solid rgba(67,97,238,.4)}
.col-row{display:grid;grid-template-columns:90px 1fr 160px 32px;gap:8px;align-items:center;margin-bottom:8px}
.col-row select,.col-row input{border:1.5px solid var(--border);border-radius:8px;padding:8px 10px;font-family:var(--sans);font-size:13px;background:var(--white);color:var(--text);width:100%}
.col-row select:focus,.col-row input:focus{outline:none;border-color:var(--accent)}
.col-row .rm{background:none;border:1.5px solid var(--border);border-radius:8px;width:32px;height:36px;cursor:pointer;font-size:16px;color:var(--muted);display:grid;place-items:center}
.col-row .rm:hover{border-color:var(--red);color:var(--red)}
.col-hdr{display:grid;grid-template-columns:90px 1fr 160px 32px;gap:8px;margin-bottom:6px}
.col-hdr span{font-size:9.5px;font-family:var(--mono);font-weight:700;color:var(--muted);text-transform:uppercase;letter-spacing:.5px}
.add-row-btn{background:none;border:1.5px dashed var(--border);border-radius:8px;width:100%;padding:9px;font-size:12.5px;font-weight:600;color:var(--muted);cursor:pointer;transition:.15s;margin-top:4px}
.add-row-btn:hover{border-color:var(--accent);color:var(--accent);background:var(--al)}
.tbl-input{border:1.5px solid var(--border);border-radius:8px;padding:10px 14px;font-family:var(--mono);font-size:14px;font-weight:600;width:100%;background:var(--white);color:var(--text)}
.tbl-input:focus{outline:none;border-color:var(--accent)}
.sql-out{background:#13141f;color:#9aa5d4;padding:16px;border-radius:10px;font-family:var(--mono);font-size:11.5px;line-height:1.7;overflow-x:auto;white-space:pre;margin-bottom:14px}
.copy-btn{background:var(--al);border:1px solid #c7d2fe;border-radius:8px;padding:8px 16px;font-size:12px;font-weight:700;color:var(--accent);cursor:pointer;font-family:var(--sans)}
.copy-btn:hover{background:#c7d2fe}
.err-box{background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:10px 14px;color:var(--red);font-size:11.5px;font-family:var(--mono);margin-top:10px}
</style>
</head>
<body>
<div class="top">
  <div class="brand"><div class="bdot"></div><div class="bname">V2 Retail · <span>RFC Pipeline</span></div><div class="btag">LIVE</div></div>
  <div class="nav" style="display:flex;align-items:center;gap:4px">
    <a id="tab-deploy" class="tab-link active" href="javascript:void(0)" onclick="showTab('deploy')">⚡ Deploy RFC</a>
    <a id="tab-columns" class="tab-link" href="javascript:void(0)" onclick="showTab('columns')">🗃️ Manage Columns</a>
    <a href="https://v2-rfc-portal.pages.dev/rfc_hub" target="_blank" style="margin-left:8px">RFC Portal →</a>
  </div>
</div>

<div class="app" id="app">

  <!-- ── DEPLOY RFC TAB ── -->
  <div id="panel-deploy">
  <div class="hdr">
    <div class="pill"><div class="dot"></div>UPLOAD · PARSE · DEPLOY · DONE</div>
    <h1>SAP RFC <em>→ Live API</em></h1>
    <p>upload rfc .docx → get a working rest api in under 2 minutes</p>
  </div>

  <div class="flow">
    <div class="fl">📄 Upload .docx</div><div class="arr">→</div>
    <div class="fl">🤖 Claude parses</div><div class="arr">→</div>
    <div class="fl">⚙️ C# generated</div><div class="arr">→</div>
    <div class="fl">📦 GitHub push</div><div class="arr">→</div>
    <div class="fl">🌐 Live API</div>
  </div>

  <div id="upload-section">
    <div class="card">
      <div class="ct">RFC Document</div>
      <div class="drop" id="drop"
           ondragover="event.preventDefault();this.classList.add('over')"
           ondragleave="this.classList.remove('over')"
           ondrop="handleDrop(event)">
        <div class="drop-icon">📄</div>
        <div class="drop-title">Drop your RFC document here</div>
        <div class="drop-sub">or <strong style="color:var(--accent);text-decoration:underline;cursor:pointer">click here to browse</strong></div>
        <div class="drop-sub" style="margin-top:4px;font-size:10px;color:#94a3b8">.docx · .txt · .md</div>
        <input type="file" id="fileInput" accept=".docx,.txt,.md" onchange="handleFile(this.files[0])">
      </div>
      <button onclick="document.getElementById('fileInput').click()"
        style="margin-top:10px;width:100%;padding:10px;background:#f1f5f9;border:1.5px dashed #cbd5e1;border-radius:8px;color:#475569;font-weight:600;cursor:pointer;font-size:13px">
        📁 Browse Files
      </button>
      <div class="file-sel" id="fileSel" style="display:none">
        <span>📎</span><span id="fileName"></span>
      </div>
    </div>

    <div class="card">
      <div class="ct">SAP Environment</div>
      <div class="env-g">
        <div class="env-c sel" id="env-dev" onclick="selEnv('dev')">
          <div class="env-label">Dev</div>
          <div class="env-sub">192.168.144.174 · 210</div>
        </div>
        <div class="env-c" id="env-quality" onclick="selEnv('quality')">
          <div class="env-label">Quality</div>
          <div class="env-sub">192.168.144.179 · 600</div>
        </div>
        <div class="env-c" id="env-production" onclick="selEnv('production')">
          <div class="env-label" style="color:#dc2626">Production</div>
          <div class="env-sub">192.168.144.170 · 600</div>
        </div>
      </div>
    </div>

    <button class="btn" id="deployBtn" onclick="deploy()" style="opacity:.45;pointer-events:none" id="deployBtn">
      ⚡ Deploy RFC → Live API
    </button>
    <div class="err-box" id="errBox" style="display:none"></div>
  </div>

  <div id="progress-section" style="display:none">
    <div class="card">
      <div class="ct">Pipeline Progress</div>
      <div class="steps" id="steps">
        <div class="step" id="s-parse"><div class="step-icon">○</div><div><div>Parse RFC document</div></div></div>
        <div class="step" id="s-controller"><div class="step-icon">○</div><div><div>Generate ASP.NET controller</div></div></div>
        <div class="step" id="s-github"><div class="step-icon">○</div><div><div>Push to GitHub</div></div></div>
        <div class="step" id="s-deploy"><div class="step-icon">○</div><div><div>Build &amp; Deploy to IIS (.36 — V2DC-ADDVERB)</div></div></div>
        <div class="step" id="s-dab"><div class="step-icon">○</div><div><div>Register in Azure DAB</div></div></div>
        <div class="step" id="s-swagger"><div class="step-icon">○</div><div><div>Update Swagger docs</div></div></div>
      </div>
    </div>
  </div>

  <div id="result-section" style="display:none">
    <div class="card">
      <div class="result">
        <div class="result-icon">🚀</div>
        <h2 id="resultTitle">RFC Deployed!</h2>
        <p id="resultSub">Your RFC is now a live REST API</p>
        <div class="ep-box" id="epBox"></div>
        <div class="chips" id="chips"></div>
        <button class="btn-reset" onclick="reset()">+ Deploy Another RFC</button>
      </div>
    </div>
  </div>
  </div><!-- /panel-deploy -->

  <!-- ── MANAGE COLUMNS TAB ── -->
  <div id="panel-columns" style="display:none">
    <div class="hdr">
      <div class="pill"><div class="dot"></div>TABLE · ADD / REMOVE COLUMNS · PUSH SQL</div>
      <h1>Data Lake <em>Column Manager</em></h1>
      <p>add or remove columns from any rfc data lake table instantly</p>
    </div>

    <div id="col-form">
      <div class="card">
        <div class="ct">Table / RFC Name</div>
        <input class="tbl-input" id="colTable" placeholder="e.g. ET_ZADVANCE_PAYMENT or ZADVANCE_PAYMENT_RFC" />
        <div style="font-size:10.5px;color:var(--muted);font-family:var(--mono);margin-top:6px">SQL table name in the data lake — auto-prefixes ET_ if needed</div>
      </div>

      <div class="card">
        <div class="ct">Column Changes</div>
        <div class="col-hdr">
          <span>Action</span><span>Column Name</span><span>SQL Type (ADD only)</span><span></span>
        </div>
        <div id="col-rows"></div>
        <button class="add-row-btn" onclick="addColRow()">+ Add another column</button>
      </div>

      <button class="btn" id="colBtn" onclick="submitColumns()">
        ⚡ Generate SQL &amp; Push to GitHub
      </button>
      <div class="err-box" id="colErr" style="display:none"></div>
    </div>

    <div id="col-result" style="display:none">
      <div class="card">
        <div class="ct">Migration SQL — Run on Azure SQL Server</div>
        <div class="sql-out" id="sqlOut"></div>
        <button class="copy-btn" onclick="copySql()">📋 Copy SQL</button>
        <div style="font-size:11px;color:var(--muted);margin-top:8px;font-family:var(--mono)">Run on SQL Server at 192.168.144.x (via Azure VPN)</div>
      </div>
      <div class="card">
        <div class="ct">What Changed</div>
        <div id="colActions" style="font-size:13px;line-height:2.2;font-family:var(--mono);color:var(--sub)"></div>
      </div>
      <button class="btn-reset" style="width:100%;margin-top:10px" onclick="resetColumns()">+ Manage Another Table</button>
    </div>
  </div><!-- /panel-columns -->

</div>

<script>
// ── TAB SWITCHING
function showTab(tab) {
  ['deploy','columns'].forEach(t => {
    document.getElementById('panel-'+t).style.display = t===tab ? 'block' : 'none';
    document.getElementById('tab-'+t).classList.toggle('active', t===tab);
  });
}

// ── COLUMN MANAGER
const SQL_TYPES = ['NVARCHAR(255)','NVARCHAR(50)','NVARCHAR(MAX)','INT','BIGINT','DECIMAL(18,2)','DECIMAL(10,4)','DATE','DATETIME','BIT','FLOAT','MONEY','UNIQUEIDENTIFIER'];
let colRowCount = 0;
function addColRow(defaultAction) {
  defaultAction = defaultAction || 'ADD';
  colRowCount++;
  var id = 'cr' + colRowCount;
  var opts = SQL_TYPES.map(function(t){ return '<option>' + t + '</option>'; }).join('');
  var row = document.createElement('div');
  row.className = 'col-row';
  row.id = id;
  var sel = document.createElement('select');
  sel.setAttribute('onchange', "toggleType('" + id + "',this.value)");
  sel.innerHTML = '<option' + (defaultAction === 'ADD' ? ' selected' : '') + '>ADD</option>'
    + '<option' + (defaultAction === 'REMOVE' ? ' selected' : '') + '>REMOVE</option>';
  var inp = document.createElement('input');
  inp.placeholder = 'COLUMN_NAME';
  inp.setAttribute('oninput', 'this.value=this.value.toUpperCase()');
  var typeSel = document.createElement('select');
  typeSel.innerHTML = opts;
  var btn = document.createElement('button');
  btn.className = 'rm';
  btn.setAttribute('onclick', "document.getElementById('" + id + "').remove()");
  btn.textContent = '\u00d7';
  row.appendChild(sel);
  row.appendChild(inp);
  row.appendChild(typeSel);
  row.appendChild(btn);
  document.getElementById('col-rows').appendChild(row);
  toggleType(id, defaultAction);
}
function toggleType(rowId, action) {
  const row = document.getElementById(rowId);
  if (!row) return;
  const ts = row.querySelectorAll('select')[1];
  ts.style.opacity = action==='REMOVE'?'0.3':'1';
  ts.disabled = action==='REMOVE';
}
async function submitColumns() {
  const table = document.getElementById('colTable').value.trim();
  if (!table) { showColErr('Please enter a table name'); return; }
  const rows = [...document.querySelectorAll('#col-rows .col-row')];
  const operations = rows.map(row => {
    const sels=row.querySelectorAll('select'), inp=row.querySelector('input');
    return {action:sels[0].value, column:inp.value.trim(), sqlType:sels[1].value};
  }).filter(op=>op.column);
  if (!operations.length) { showColErr('Add at least one column name'); return; }
  document.getElementById('colBtn').disabled=true;
  document.getElementById('colBtn').innerHTML='<span class="spin"></span> Pushing to GitHub...';
  document.getElementById('colErr').style.display='none';
  try {
    const r = await fetch('/columns',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({tableName:table,operations})});
    const d = await r.json();
    if (!r.ok) throw new Error(d.error||'Failed');
    showColResult(d, operations);
  } catch(e) {
    showColErr(e.message);
    document.getElementById('colBtn').disabled=false;
    document.getElementById('colBtn').innerHTML='⚡ Generate SQL &amp; Push to GitHub';
  }
}
function showColErr(msg){const el=document.getElementById('colErr');el.textContent=msg;el.style.display='block';}
function showColResult(d, operations) {
  document.getElementById('col-form').style.display='none';
  document.getElementById('col-result').style.display='block';
  document.getElementById('sqlOut').textContent = d.sql;
  let html='';
  operations.forEach(op=>{
    html+=(op.action==='ADD'?'<span style="color:var(--green)">✅ ADD</span>':'<span style="color:var(--red)">🗑️ REMOVE</span>')+' <b>'+op.column+'</b>'+(op.action==='ADD'?' <span style="color:var(--muted)">'+op.sqlType+'</span>':'')+'<br>';
  });
  if(d.commitUrl) html+='<br>📦 <a href="'+d.commitUrl+'" target="_blank" style="color:var(--accent)">View GitHub commit →</a><br>';
  if(d.dabUpdated) html+='🔄 DAB config updated in GitHub<br>';
  html+='<br><span style="color:var(--muted);font-size:10.5px">Migration saved to: '+d.migPath+'</span>';
  document.getElementById('colActions').innerHTML=html;
}
function copySql(){
  navigator.clipboard.writeText(document.getElementById('sqlOut').textContent)
    .then(()=>{const b=document.querySelector('.copy-btn');b.textContent='✓ Copied!';setTimeout(()=>b.textContent='📋 Copy SQL',2000);});
}
function resetColumns(){
  document.getElementById('col-result').style.display='none';
  document.getElementById('col-form').style.display='block';
  document.getElementById('colTable').value='';
  document.getElementById('col-rows').innerHTML='';
  document.getElementById('colBtn').disabled=false;
  document.getElementById('colBtn').innerHTML='⚡ Generate SQL &amp; Push to GitHub';
  colRowCount=0; addColRow();
}
window.addEventListener('DOMContentLoaded',()=>addColRow());

let selectedFile = null;
let selectedEnv  = 'dev';
let pollTimer    = null;

window.handleFile = function handleFile(file) {
  if (!file) return;
  selectedFile = file;
  document.getElementById('fileSel').style.display='flex';
  document.getElementById('fileName').textContent = file.name;
  document.getElementById('drop').classList.remove('over');
  var btn = document.getElementById('deployBtn');
  btn.style.opacity = '1';
  btn.style.pointerEvents = 'auto';
  btn.style.animation = 'pulse 0.3s ease';
}
function handleDrop(e) {
  e.preventDefault();
  document.getElementById('drop').classList.remove('over');
  handleFile(e.dataTransfer.files[0]);
}
window.selEnv = function selEnv(env) {
  selectedEnv = env;
  ['dev','quality','production'].forEach(e => {
    document.getElementById('env-'+e).classList.toggle('sel', e===env);
  });
}

window.deploy = async function deploy() {
  if (!selectedFile) { alert('Please select a .docx file first'); return; }
  document.getElementById('errBox').style.display='none';
  document.getElementById('upload-section').style.display='none';
  document.getElementById('progress-section').style.display='block';

  const fd = new FormData();
  fd.append('file', selectedFile);
  fd.append('env', selectedEnv);

  try {
    const r = await fetch('/deploy', {method:'POST', body:fd});
    const d = await r.json();
    if (!r.ok) throw new Error(d.error||'Deploy failed');
    pollStatus(d.jobId);
  } catch(e) {
    document.getElementById('upload-section').style.display='block';
    document.getElementById('progress-section').style.display='none';
    document.getElementById('errBox').textContent='Error: '+e.message;
    document.getElementById('errBox').style.display='block';
  }
}

function updateStep(id, status, detail) {
  const el = document.getElementById('s-'+id);
  if (!el) return;
  el.className = 'step ' + (status==='running'?'run':status==='done'?'done':status==='error'?'error':'');
  const icon = el.querySelector('.step-icon');
  icon.innerHTML = status==='running'?'<span class="spin"/>':status==='done'?'✓':status==='error'?'✗':'○';
  const content = el.querySelector('div:last-child');
  if (detail) {
    let det = content.querySelector('.step-detail');
    if (!det) { det=document.createElement('div'); det.className='step-detail'; content.appendChild(det); }
    det.textContent = detail;
  }
}

window.pollStatus = function pollStatus(jobId) {
  pollTimer = setInterval(async () => {
    try {
      const r = await fetch('/status/'+jobId);
      const job = await r.json();
      (job.steps||[]).forEach(s => updateStep(s.step, s.status, s.detail));
      if (job.status==='complete') {
        clearInterval(pollTimer);
        showResult(job);
      } else if (job.status==='error') {
        clearInterval(pollTimer);
        document.getElementById('progress-section').style.display='none';
        document.getElementById('upload-section').style.display='block';
        document.getElementById('errBox').textContent='Pipeline error: '+(job.error||'Unknown error');
        document.getElementById('errBox').style.display='block';
      }
    } catch(e) {}
  }, 1500);
}

function showResult(job) {
  document.getElementById('progress-section').style.display='none';
  document.getElementById('result-section').style.display='block';
  document.getElementById('resultTitle').textContent = (job.rfcName||'RFC') + ' Deployed!';
  document.getElementById('resultSub').textContent = 'Your RFC is now a live REST API with data lake access';
  document.getElementById('epBox').innerHTML =
    '<span>RFC API (live on IIS)</span>' + (job.rfcApi||'') +
    '<span style="margin-top:8px">Data Lake REST API (Azure DAB)</span>' + (job.dataLake||'') +
    '?$filter=FIELD eq \'value\'&$top=100';
  const chips = document.getElementById('chips');
  chips.innerHTML = '';
  if (job.commit) chips.innerHTML += '<a class="chip g" href="'+job.commit+'" target="_blank">✓ GitHub commit</a>';
  if (job.dataLake) chips.innerHTML += '<a class="chip b" href="'+job.dataLake+'" target="_blank">📡 Data Lake API</a>';
  if (job.swagger) chips.innerHTML += '<a class="chip b" href="'+job.swagger+'" target="_blank">📖 Swagger UI</a>';
}

function reset() {
  selectedFile=null; selectedEnv='dev';
  document.getElementById('result-section').style.display='none';
  document.getElementById('upload-section').style.display='block';
  document.getElementById('fileSel').style.display='none';
  document.getElementById('deployBtn').disabled=true;
  selEnv('dev');
  ['parse','controller','github','deploy','dab','swagger'].forEach(s=>{
    const el=document.getElementById('s-'+s);
    if(el){el.className='step';el.querySelector('.step-icon').innerHTML='○';const d=el.querySelector('.step-detail');if(d)d.remove();}
  });
}
</script>
</body>
</html>`;

// ─── Swagger redirect page ────────────────────────────────────────────────────
const SWAGGER_HTML = `<!DOCTYPE html>
<html><head><meta charset="utf-8">
<meta http-equiv="refresh" content="0;url=https://v2-rfc-portal.pages.dev/rfc_hub">
<title>Redirecting to Swagger...</title></head>
<body style="font-family:monospace;display:grid;place-items:center;height:100vh;background:#0f172a;color:#9aa5d4">
<p>→ Redirecting to Swagger UI...</p>
</body></html>`;

// ─── Worker handler ───────────────────────────────────────────────────────────
export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);

    // GET / → upload UI
    if (url.pathname === '/' && request.method === 'GET') {
      return new Response(HTML, {headers:{'Content-Type':'text/html;charset=utf-8'}});
    }

    // GET /swagger → redirect
    if (url.pathname === '/swagger') {
      return new Response(SWAGGER_HTML, {headers:{'Content-Type':'text/html;charset=utf-8'}});
    }

    // POST /deploy → start pipeline
    if (url.pathname === '/deploy' && request.method === 'POST') {
      const formData = await request.formData();
      const file     = formData.get('file');
      const sapEnv   = formData.get('env')||'dev';

      if (!file) return new Response(JSON.stringify({error:'No file uploaded'}),
        {status:400, headers:{'Content-Type':'application/json'}});

      const jobId = crypto.randomUUID();
      const initialJob = {status:'running', steps:[], started:new Date().toISOString()};
      await env.RFC_JOBS.put(jobId, JSON.stringify(initialJob), {expirationTtl:86400});

      // Extract text from file
      let text = '';
      let docxImages = [];
      try {
        const ab = await file.arrayBuffer();
        const fname = file.name.toLowerCase();
        if (fname.endsWith('.docx')) {
          const extracted = await extractDocxText(ab);
          text = extracted.text || '';
          docxImages = extracted.images || [];
        } else {
          text = new TextDecoder().decode(ab);
        }
      } catch(e) {
        return new Response(JSON.stringify({error:'Failed to read file: '+e.message}),
          {status:400, headers:{'Content-Type':'application/json'}});
      }
      if (text.length < 50 && docxImages.length === 0) {
        return new Response(JSON.stringify({error:'Empty docx — no text or images found'}),
          {status:400, headers:{'Content-Type':'application/json'}});
      }

      // Run pipeline in background (non-blocking)
      // Use ctx.waitUntil so the Worker stays alive for the full pipeline
      ctx.waitUntil(runPipeline(text, sapEnv, jobId, env, file.name||'', docxImages));

      return new Response(JSON.stringify({jobId, status:'running'}),
        {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // GET /status/:jobId
    const match = url.pathname.match(/^\/status\/(.+)$/);
    if (match && request.method === 'GET') {
      const job = await env.RFC_JOBS.get(match[1]);
      if (!job) return new Response(JSON.stringify({error:'Job not found'}),
        {status:404, headers:{'Content-Type':'application/json'}});
      return new Response(job, {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // POST /columns → manage data lake columns
    if (url.pathname === '/columns' && request.method === 'POST') {
      try {
        const body = await request.json();
        const { tableName, operations } = body;
        if (!tableName) return new Response(JSON.stringify({error:'tableName required'}),
          {status:400,headers:{'Content-Type':'application/json'}});
        if (!operations || !operations.length) return new Response(JSON.stringify({error:'operations required'}),
          {status:400,headers:{'Content-Type':'application/json'}});
        const result = await manageColumns(tableName, operations, env.GITHUB_TOKEN);
        return new Response(JSON.stringify(result),
          {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),
          {status:500,headers:{'Content-Type':'application/json'}});
      }
    }

    // OPTIONS preflight
    if (request.method === 'OPTIONS') {
      return new Response(null, {headers:{'Access-Control-Allow-Origin':'*','Access-Control-Allow-Methods':'POST,GET','Access-Control-Allow-Headers':'Content-Type'}});
    }


    // ── SYNC ROUTES ───────────────────────────────────────────────────────────

    // GET /sync → dashboard UI
    if (url.pathname === '/sync' && request.method === 'GET') {
      return new Response(SYNC_HTML, {headers:{'Content-Type':'text/html;charset=utf-8'}});
    }

    // POST /sync/register → add a new sync job
    if (url.pathname === '/sync/register' && request.method === 'POST') {
      try {
        const { rfcName, tableName, label, params } = await request.json();
        if (!rfcName || !tableName)
          return new Response(JSON.stringify({error:'rfcName and tableName required'}),
            {status:400, headers:{'Content-Type':'application/json'}});
        const job = { rfcName, tableName, label: label||rfcName, params: params||{},
          enabled: true, registeredAt: new Date().toISOString() };
        await env.RFC_JOBS.put(`sync_job:${rfcName}`, JSON.stringify(job));
        return new Response(JSON.stringify({ok:true,job}),
          {headers:{'Content-Type':'application/json'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),
          {status:500, headers:{'Content-Type':'application/json'}});
      }
    }

    // GET /sync/jobs → list all jobs with last results
    if (url.pathname === '/sync/jobs' && request.method === 'GET') {
      const list = await env.RFC_JOBS.list({prefix:'sync_job:'});
      const jobs = await Promise.all(list.keys.map(async k => {
        const job  = JSON.parse(await env.RFC_JOBS.get(k.name) || '{}');
        const res  = await env.RFC_JOBS.get(`sync_result:${job.rfcName}`);
        job.result = res ? JSON.parse(res) : null;
        return job;
      }));
      return new Response(JSON.stringify(jobs),
        {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // POST /sync/run/:rfcName → queue trigger in KV (IIS polls and picks up)
    const syncRunMatch = url.pathname.match(/^\/sync\/run\/(.+)$/);
    if (syncRunMatch && request.method === 'POST') {
      const rfcName = syncRunMatch[1];
      const raw = await env.RFC_JOBS.get(`sync_job:${rfcName}`);
      if (!raw) return new Response(JSON.stringify({error:'Job not found'}),
        {status:404, headers:{'Content-Type':'application/json'}});
      // Set a trigger key — IIS SyncController /api/Sync/poll picks this up within 1 min
      await env.RFC_JOBS.put(`sync_trigger:${rfcName}`,
        new Date().toISOString(), { expirationTtl: 300 });
      return new Response(JSON.stringify({
        status: 'queued',
        rfcName,
        msg: 'Sync queued — IIS will execute within 1 minute via poll'
      }), {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // POST /sync/run-all → queue all triggers
    if (url.pathname === '/sync/run-all' && request.method === 'POST') {
      const list = await env.RFC_JOBS.list({prefix:'sync_job:'});
      const queued = [];
      for (const key of list.keys) {
        const job = JSON.parse(await env.RFC_JOBS.get(key.name) || '{}');
        if (job.enabled === false) continue;
        await env.RFC_JOBS.put(`sync_trigger:${job.rfcName}`,
          new Date().toISOString(), { expirationTtl: 300 });
        queued.push(job.rfcName);
      }
      return new Response(JSON.stringify({
        status: 'queued', count: queued.length, rfcNames: queued,
        msg: 'All jobs queued — IIS will execute within 1 minute via poll'
      }), {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // DELETE /sync/delete/:rfcName → remove job
    const syncDelMatch = url.pathname.match(/^\/sync\/delete\/(.+)$/);
    if (syncDelMatch && request.method === 'DELETE') {
      const rfcName = syncDelMatch[1];
      await env.RFC_JOBS.delete(`sync_job:${rfcName}`);
      await env.RFC_JOBS.delete(`sync_result:${rfcName}`);
      return new Response(JSON.stringify({ok:true}),
        {headers:{'Content-Type':'application/json'}});
    }

    // GET /explore → RFC Explorer


    // ── SYNC ROUTES ───────────────────────────────────────────────────────────

    // GET /sync → dashboard UI
    if (url.pathname === '/sync' && request.method === 'GET') {
      return new Response(SYNC_HTML, {headers:{'Content-Type':'text/html;charset=utf-8'}});
    }

    // POST /sync/register → add a new sync job
    if (url.pathname === '/sync/register' && request.method === 'POST') {
      try {
        const { rfcName, tableName, label, params } = await request.json();
        if (!rfcName || !tableName)
          return new Response(JSON.stringify({error:'rfcName and tableName required'}),
            {status:400, headers:{'Content-Type':'application/json'}});
        const job = { rfcName, tableName, label: label||rfcName, params: params||{},
          enabled: true, registeredAt: new Date().toISOString() };
        await env.RFC_JOBS.put(`sync_job:${rfcName}`, JSON.stringify(job));
        return new Response(JSON.stringify({ok:true,job}),
          {headers:{'Content-Type':'application/json'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),
          {status:500, headers:{'Content-Type':'application/json'}});
      }
    }

    // GET /sync/jobs → list all jobs with last results
    if (url.pathname === '/sync/jobs' && request.method === 'GET') {
      const list = await env.RFC_JOBS.list({prefix:'sync_job:'});
      const jobs = await Promise.all(list.keys.map(async k => {
        const job  = JSON.parse(await env.RFC_JOBS.get(k.name) || '{}');
        const res  = await env.RFC_JOBS.get(`sync_result:${job.rfcName}`);
        job.result = res ? JSON.parse(res) : null;
        return job;
      }));
      return new Response(JSON.stringify(jobs),
        {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

    // GET /explore → RFC Explorer (light theme)
    if (url.pathname === '/explore' || url.pathname === '/explore/') {
      const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>V2 Retail · RFC API Explorer</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
:root{
  --bg:#f4f6fb;
  --white:#ffffff;
  --surface:#f8f9fc;
  --border:#e2e6ef;
  --border2:#d0d7e3;
  --text:#1a2035;
  --sub:#4a5568;
  --muted:#7a8499;
  --dim:#b0b8cc;
  --accent:#2563eb;
  --accent-bg:#eff4ff;
  --accent-border:#bfcfff;
  --green:#16a34a;
  --green-bg:#f0fdf4;
  --green-border:#bbf7d0;
  --orange:#c2410c;
  --orange-bg:#fff7ed;
  --purple:#7c3aed;
  --purple-bg:#f5f3ff;
  --red:#dc2626;
  --red-bg:#fef2f2;
  --sans:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,sans-serif;
  --mono:"Consolas","SF Mono","Fira Code",monospace;
}

body{background:var(--bg);color:var(--text);font-family:var(--sans);min-height:100vh;display:flex;flex-direction:column;font-size:14px;}

/* TOP BAR */
.topbar{background:var(--white);border-bottom:1px solid var(--border);padding:0 24px;height:54px;display:flex;align-items:center;justify-content:space-between;position:sticky;top:0;z-index:100;box-shadow:0 1px 3px rgba(0,0,0,.06);}
.logo{display:flex;align-items:center;gap:10px;}
.logo-dot{width:7px;height:7px;border-radius:50%;background:var(--green);box-shadow:0 0 0 3px var(--green-bg);animation:pulse 2.5s infinite;}
@keyframes pulse{0%,100%{box-shadow:0 0 0 3px var(--green-bg)}50%{box-shadow:0 0 0 6px #dcfce7}}
.logo-text{font-weight:700;font-size:15px;color:var(--text);letter-spacing:-.3px;}
.logo-text span{color:var(--accent);}
.logo-badge{font-family:var(--mono);font-size:10px;background:var(--accent-bg);border:1px solid var(--accent-border);color:var(--accent);padding:2px 8px;border-radius:4px;font-weight:500;}
.topbar-right{display:flex;align-items:center;gap:20px;}
.stat{font-size:12px;color:var(--muted);display:flex;align-items:center;gap:4px;}
.stat strong{color:var(--text);font-weight:600;}
.live-pill{display:flex;align-items:center;gap:5px;font-size:11px;font-weight:600;color:var(--green);background:var(--green-bg);border:1px solid var(--green-border);padding:3px 10px;border-radius:20px;}

/* LAYOUT */
.layout{display:flex;flex:1;overflow:hidden;height:calc(100vh - 54px);}

/* SIDEBAR */
.sidebar{width:256px;flex-shrink:0;background:var(--white);border-right:1px solid var(--border);display:flex;flex-direction:column;overflow:hidden;}
.search-wrap{padding:12px;border-bottom:1px solid var(--border);position:relative;}
.search-icon{position:absolute;left:22px;top:50%;transform:translateY(-50%);color:var(--dim);font-size:14px;}
.search{width:100%;background:var(--surface);border:1.5px solid var(--border);border-radius:7px;padding:7px 10px 7px 30px;color:var(--text);font-size:12.5px;font-family:var(--sans);outline:none;transition:.15s;}
.search:focus{border-color:var(--accent);background:var(--white);box-shadow:0 0 0 3px rgba(37,99,235,.08);}
.search::placeholder{color:var(--dim);}
.sidebar-list{overflow-y:auto;flex:1;}
.sidebar-list::-webkit-scrollbar{width:4px;}
.sidebar-list::-webkit-scrollbar-thumb{background:var(--border2);border-radius:2px;}

.group-header{padding:8px 14px 4px;font-size:9.5px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase;color:var(--dim);display:flex;align-items:center;justify-content:space-between;cursor:pointer;user-select:none;margin-top:4px;}
.group-header:hover .group-label{color:var(--muted);}
.group-count{background:var(--surface);border:1px solid var(--border);border-radius:8px;padding:0 6px;font-size:9px;color:var(--dim);}
.group-endpoints{}
.ep-item{padding:6px 14px 6px 20px;font-size:12px;color:var(--sub);cursor:pointer;border-left:2px solid transparent;transition:.12s;display:flex;align-items:center;gap:7px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;}
.ep-item:hover{color:var(--text);background:var(--surface);}
.ep-item.active{color:var(--accent);border-left-color:var(--accent);background:var(--accent-bg);font-weight:500;}
.ep-dot{width:4px;height:4px;border-radius:50%;background:var(--green);flex-shrink:0;}
.ep-item.active .ep-dot{background:var(--accent);}

/* MAIN */
.main{flex:1;overflow-y:auto;padding:28px 32px;}
.main::-webkit-scrollbar{width:6px;}
.main::-webkit-scrollbar-thumb{background:var(--border2);border-radius:3px;}

/* WELCOME */
.welcome{max-width:600px;margin:64px auto;text-align:center;}
.welcome-icon{font-size:44px;margin-bottom:16px;}
.welcome h2{font-size:22px;font-weight:700;color:var(--text);margin-bottom:8px;}
.welcome p{color:var(--muted);font-size:13.5px;line-height:1.75;}
.welcome-stats{display:flex;gap:16px;justify-content:center;margin-top:28px;flex-wrap:wrap;}
.wstat{background:var(--white);border:1px solid var(--border);border-radius:10px;padding:14px 22px;text-align:center;box-shadow:0 1px 3px rgba(0,0,0,.04);}
.wstat .num{font-size:26px;font-weight:700;color:var(--accent);display:block;font-family:var(--mono);}
.wstat .lbl{font-size:11px;color:var(--muted);margin-top:2px;}

/* ENDPOINT DETAIL */
.ep-header{margin-bottom:20px;}
.ep-title-row{display:flex;align-items:center;gap:10px;flex-wrap:wrap;margin-bottom:6px;}
.method-badge{font-family:var(--mono);font-size:11px;font-weight:600;background:var(--green-bg);color:var(--green);border:1.5px solid var(--green-border);padding:3px 10px;border-radius:5px;}
.ep-name{font-size:22px;font-weight:700;color:var(--text);}
.ep-meta{font-size:12px;color:var(--muted);margin-bottom:8px;}
.ep-meta span{color:var(--purple);font-family:var(--mono);}
.ep-route-box{display:inline-flex;align-items:center;gap:8px;background:var(--orange-bg);border:1px solid #fed7aa;border-radius:7px;padding:7px 14px;font-family:var(--mono);font-size:12.5px;}
.ep-route-base{color:var(--muted);}
.ep-route-path{color:var(--orange);}
.source-link{display:inline-flex;align-items:center;gap:5px;font-size:11.5px;color:var(--muted);text-decoration:none;margin-top:8px;}
.source-link:hover{color:var(--accent);}

/* CARDS */
.card{background:var(--white);border:1px solid var(--border);border-radius:10px;margin-bottom:14px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.04);}
.card-header{padding:10px 16px;background:var(--surface);border-bottom:1px solid var(--border);display:flex;align-items:center;justify-content:space-between;}
.card-title{font-size:10px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase;color:var(--muted);display:flex;align-items:center;gap:8px;}
.card-count{background:var(--white);border:1px solid var(--border);border-radius:8px;padding:0 7px;font-size:10px;color:var(--muted);}

/* PARAM TABLE */
.param-table{width:100%;border-collapse:collapse;}
.param-table th{padding:8px 16px;font-size:10px;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;color:var(--muted);text-align:left;border-bottom:1px solid var(--border);background:var(--surface);}
.param-table td{padding:9px 16px;font-size:12.5px;border-bottom:1px solid var(--border);color:var(--sub);}
.param-table tr:last-child td{border-bottom:none;}
.param-table tr:hover td{background:#fafbff;}
.pname{color:var(--accent);font-family:var(--mono);}
.ptype{color:var(--muted);font-family:var(--mono);font-size:11.5px;}
.pbadge{display:inline-flex;font-size:10px;font-weight:600;background:var(--green-bg);color:var(--green);border:1px solid var(--green-border);padding:1px 7px;border-radius:4px;font-family:var(--mono);}
.no-data{padding:20px 16px;font-size:12.5px;color:var(--dim);font-style:italic;}

/* TABLE CHIPS */
.chips{padding:12px 16px;display:flex;flex-wrap:wrap;gap:8px;}
.chip{font-family:var(--mono);font-size:11.5px;background:var(--orange-bg);border:1px solid #fed7aa;color:var(--orange);padding:4px 11px;border-radius:5px;}

/* COLUMN MANAGER */
.col-body{padding:16px;}
.col-form{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:12px;}
.col-field label{font-size:10.5px;font-weight:600;letter-spacing:1px;text-transform:uppercase;color:var(--muted);display:block;margin-bottom:5px;}
.col-input{width:100%;background:var(--white);border:1.5px solid var(--border);border-radius:7px;padding:8px 11px;color:var(--text);font-size:12.5px;font-family:var(--sans);outline:none;transition:.15s;}
.col-input:focus{border-color:var(--accent);box-shadow:0 0 0 3px rgba(37,99,235,.07);}
.col-input::placeholder{color:var(--dim);}
.col-btns{display:flex;gap:8px;margin-bottom:12px;}
.btn-add{flex:1;background:var(--green);color:#fff;border:none;border-radius:7px;padding:9px;font-size:12.5px;font-weight:600;cursor:pointer;transition:.15s;}
.btn-add:hover{background:#15803d;}
.btn-rem{flex:1;background:var(--red-bg);color:var(--red);border:1.5px solid #fecaca;border-radius:7px;padding:9px;font-size:12.5px;font-weight:600;cursor:pointer;transition:.15s;}
.btn-rem:hover{background:#fee2e2;}
.ops-list{display:flex;flex-direction:column;gap:6px;margin-bottom:12px;}
.op-row{display:flex;align-items:center;gap:8px;background:var(--surface);border:1px solid var(--border);border-radius:7px;padding:8px 12px;font-size:12.5px;}
.op-add{color:var(--green);font-weight:700;}
.op-rem{color:var(--red);font-weight:700;}
.op-col{color:var(--accent);font-family:var(--mono);}
.op-type{color:var(--muted);font-family:var(--mono);font-size:11.5px;}
.op-del{margin-left:auto;background:none;border:none;color:var(--dim);cursor:pointer;font-size:15px;line-height:1;padding:0 2px;}
.op-del:hover{color:var(--red);}
.btn-apply{width:100%;background:var(--accent);color:#fff;border:none;border-radius:7px;padding:10px;font-size:13px;font-weight:600;cursor:pointer;transition:.15s;display:flex;align-items:center;justify-content:center;gap:7px;}
.btn-apply:hover{background:#1d4ed8;}
.btn-apply:disabled{opacity:.45;cursor:not-allowed;}
.result-box{margin-top:10px;padding:10px 14px;border-radius:7px;font-size:12.5px;display:none;}
.result-ok{background:var(--green-bg);border:1px solid var(--green-border);color:var(--green);}
.result-err{background:var(--red-bg);border:1px solid #fecaca;color:var(--red);}
</style>
</head>
<body>

<div class="topbar">
  <div class="logo">
    <div class="logo-dot"></div>
    <div class="logo-text">V2 Retail <span>·</span> RFC Explorer</div>
    <div class="logo-badge">IIS → SAP</div>
  </div>
  <div class="topbar-right">
    <div class="stat"><strong id="tc">–</strong>&nbsp;endpoints</div>
    <div class="stat"><strong id="gc">–</strong>&nbsp;modules</div>
    <div class="live-pill">● LIVE</div>
  </div>
</div>

<div class="layout">
  <div class="sidebar">
    <div class="search-wrap">
      <span class="search-icon">🔍</span>
      <input class="search" id="srch" placeholder="Search RFCs, params, tables..." oninput="doSearch()">
    </div>
    <div class="sidebar-list" id="sidebarList"></div>
  </div>

  <div class="main">
    <div id="welcome">
      <div class="welcome">
        <div class="welcome-icon">⚙️</div>
        <h2>RFC API Explorer</h2>
        <p>All SAP RFC endpoints from your IIS codebase — parameters, response tables, and data lake column management in one place.</p>
        <div class="welcome-stats">
          <div class="wstat"><span class="num" id="ws1">–</span><div class="lbl">Total RFCs</div></div>
          <div class="wstat"><span class="num" id="ws2">–</span><div class="lbl">Modules</div></div>
          <div class="wstat"><span class="num" id="ws3">–</span><div class="lbl">Parameters</div></div>
          <div class="wstat"><span class="num" id="ws4">–</span><div class="lbl">SAP Tables</div></div>
        </div>
      </div>
    </div>
    <div id="detail" style="display:none"></div>
  </div>
</div>

<script>
const ENDPOINTS = [{"id":"articleotb","name":"Article_OTB","folder":"BroaderMenu","group":"Broader Menu","rfc":"ZPLC_PO_RFC","route":"api/Article_OTB","method":"POST","params":[{"name":"IM_MATNR_FROM","in":"body","type":"string"},{"name":"articleno","in":"body","type":"string"},{"name":"Material_Group","in":"body","type":"string"},{"name":"Segment","in":"body","type":"string"},{"name":"Division","in":"body","type":"string"},{"name":"Sub_Division","in":"body","type":"string"},{"name":"Major_Category","in":"body","type":"string"},{"name":"Major_Category_Status","in":"body","type":"string"},{"name":"Sub_Category_Description","in":"body","type":"string"},{"name":"MC_Description","in":"body","type":"string"},{"name":"Season","in":"body","type":"string"},{"name":"MVGR","in":"body","type":"string"},{"name":"MVGR_Value","in":"body","type":"string"},{"name":"PO_RAISING_MONTH","in":"body","type":"string"},{"name":"max_opt_in_maj_cat","in":"body","type":"string"},{"name":"AUTO_CONT","in":"body","type":"string"},{"name":"BGT_CONT","in":"body","type":"string"},{"name":"Year","in":"body","type":"string"},{"name":"BGT_OPT_CNT","in":"body","type":"string"},{"name":"BGT_PO_QTY","in":"body","type":"string"},{"name":"BGT_PO_VALUE","in":"body","type":"string"},{"name":"MVGR_COUNT","in":"body","type":"string"},{"name":"MVGR_OPT_COUNT","in":"body","type":"string"},{"name":"TAG_QTY","in":"body","type":"string"},{"name":"PO_QTY","in":"body","type":"string"},{"name":"PO_VAL","in":"body","type":"string"},{"name":"OPT_BAL","in":"body","type":"string"},{"name":"QTY_OTB","in":"body","type":"string"},{"name":"VAL_OTB","in":"body","type":"string"},{"name":"TAG_VAL","in":"body","type":"string"}],"tables":["TT_DATA","EX_RETURN"],"path":"Controllers/BroaderMenu/Article_OTBController.cs"},{"id":"zdcroutingsubrfc","name":"ZDC_ROUTING_SUB_RFC","folder":"DcRouting","group":"DC Routing","rfc":"ZDC_ROUTING_SUB_RFC","route":"api/ZDC_ROUTING_SUB_RFC","method":"POST","params":[{"name":"IM_DC_ROUTING","in":"body","type":"string"},{"name":"IM_GATE_ENTRY","in":"body","type":"string"},{"name":"IM_EBELN","in":"body","type":"string"},{"name":"BGT_START_DATE","in":"body","type":"string"},{"name":"BGT_END_DATE","in":"body","type":"string"},{"name":"ACT_START_DATE","in":"body","type":"string"},{"name":"ACT_END_DATE","in":"body","type":"string"},{"name":"START_TM","in":"body","type":"string"},{"name":"END_TIME","in":"body","type":"string"},{"name":"PREPARED_BY","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"},{"name":"BOX_TOTAL","in":"body","type":"string"},{"name":"TTL_STAGING_QTY","in":"body","type":"string"},{"name":"ACT_VAL","in":"body","type":"string"},{"name":"STAG_SEC","in":"body","type":"string"},{"name":"BIN_COUNT","in":"body","type":"string"},{"name":"ZRFS_QTY","in":"body","type":"string"},{"name":"PLTNO","in":"body","type":"string"},{"name":"CRT_NO","in":"body","type":"string"},{"name":"QC_DONE_QTY","in":"body","type":"string"},{"name":"QC_FAILED_QTY","in":"body","type":"string"},{"name":"LOT_STATUS","in":"body","type":"string"},{"name":"PHY_SAP_PO","in":"body","type":"string"},{"name":"PROCESS_TODO","in":"body","type":"string"},{"name":"COLOUR","in":"body","type":"string"},{"name":"BARCODE_STAT","in":"body","type":"string"},{"name":"ZSIZE","in":"body","type":"string"},{"name":"FABRIC_DTL","in":"body","type":"string"},{"name":"QC_BY","in":"body","type":"string"},{"name":"LOT_ATR","in":"body","type":"string"},{"name":"APPROVED_BY","in":"body","type":"string"},{"name":"PROC_ST_NO","in":"body","type":"string"},{"name":"IS_LOCK_NO","in":"body","type":"string"},{"name":"IS_LOG_DATE","in":"body","type":"string"},{"name":"IS_LOG_TM","in":"body","type":"string"},{"name":"IS_DONE_DT","in":"body","type":"string"},{"name":"IS_DONE_TM","in":"body","type":"string"},{"name":"BARCD_STAT","in":"body","type":"string"},{"name":"PACK_SIZE_QTY","in":"body","type":"string"},{"name":"SCANNER_NAME","in":"body","type":"string"},{"name":"AC_QTY_OLD","in":"body","type":"string"},{"name":"AC_QTY_NEW","in":"body","type":"string"},{"name":"TT_TD_QTY","in":"body","type":"string"},{"name":"ACT_REC_CRATE","in":"body","type":"string"},{"name":"DONE_CRATE","in":"body","type":"string"},{"name":"BGT_SAM","in":"body","type":"string"},{"name":"ACT_SAM","in":"body","type":"string"},{"name":"WITH_PACK_SIZE","in":"body","type":"string"},{"name":"GRC_NO","in":"body","type":"string"},{"name":"GRC_QTY","in":"body","type":"string"},{"name":"GRC_VAL","in":"body","type":"string"},{"name":"REMARKSS","in":"body","type":"string"},{"name":"BIN_NO","in":"body","type":"string"},{"name":"LOT_QTY","in":"body","type":"string"},{"name":"SAMPLE_REC","in":"body","type":"string"},{"name":"ACT_REC_DT","in":"body","type":"string"},{"name":"ACT_REC_TIME","in":"body","type":"string"},{"name":"ACT_REC_HU","in":"body","type":"string"},{"name":"DONE_QTY","in":"body","type":"string"},{"name":"PACK_SIZE_BGT_QTY","in":"body","type":"string"},{"name":"PACK_SIZE_ACT_QTY","in":"body","type":"string"},{"name":"MP_USED","in":"body","type":"string"},{"name":"DONE_CARET","in":"body","type":"string"},{"name":"BAL_QTY","in":"body","type":"string"},{"name":"QTY_VAL","in":"body","type":"string"},{"name":"QC_DONE","in":"body","type":"string"},{"name":"PROCESS_STAT","in":"body","type":"string"},{"name":"ACT_REC_Q","in":"body","type":"string"},{"name":"PROC_TAT","in":"body","type":"string"},{"name":"EFFECIENCY","in":"body","type":"string"},{"name":"GE_NO","in":"body","type":"string"},{"name":"DCNO","in":"body","type":"string"},{"name":"TEXT","in":"body","type":"string"},{"name":"EMP_CD","in":"body","type":"string"},{"name":"PO_NUM","in":"body","type":"string"},{"name":"MAN_PO","in":"body","type":"string"},{"name":"ASS_QTY","in":"body","type":"string"},{"name":"ACT_DONE","in":"body","type":"string"},{"name":"ACT_ST_DT","in":"body","type":"string"},{"name":"ACT_ST_TM","in":"body","type":"string"},{"name":"ACT_END_DT","in":"body","type":"string"},{"name":"ACT_END_TM","in":"body","type":"string"},{"name":"EBELN","in":"body","type":"string"},{"name":"VND_CONFM","in":"body","type":"string"},{"name":"CLR_SIZE","in":"body","type":"string"},{"name":"PACK_NORMS","in":"body","type":"string"},{"name":"PACK_SIZE","in":"body","type":"string"},{"name":"TAFETA_BARCODE","in":"body","type":"string"},{"name":"BARCODE_VEND","in":"body","type":"string"},{"name":"APPR_QUALITY","in":"body","type":"string"},{"name":"EACH_DESIGN","in":"body","type":"string"},{"name":"PRIVATE_LABEL","in":"body","type":"string"},{"name":"PIC_PR_LABEL","in":"body","type":"string"},{"name":"CARTON","in":"body","type":"string"},{"name":"BUYING","in":"body","type":"string"},{"name":"SAMPLES","in":"body","type":"string"},{"name":"PPT_TYPE","in":"body","type":"string"},{"name":"IM_ROUTING_NO","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"},{"name":"IM_GRC","in":"body","type":"string"},{"name":"IM_ASN","in":"body","type":"string"},{"name":"BGT_ST_DT","in":"body","type":"string"},{"name":"BGT_END_DT","in":"body","type":"string"},{"name":"BGT_ST_TM","in":"body","type":"string"},{"name":"BGT_END_TM","in":"body","type":"string"}],"tables":["LT_PROD","LT_DATA","LS_GET_DATA","LS_OT_DETAILS","LS_COMP","EX_RETURN"],"path":"Controllers/DcRouting/ZDC_ROUTING_SUB_RFCController.cs"},{"id":"zwmgateentryrfc","name":"ZWM_GATE_ENTRY_RFC","folder":"DcRouting","group":"DC Routing","rfc":"ZWM_GATE_ENTRY_RFC","route":"api/ZWM_GATE_ENTRY_RFC","method":"POST","params":[{"name":"IM_EBELN","in":"body","type":"string"},{"name":"Gate_Entry_No","in":"body","type":"string"},{"name":"Quantity","in":"body","type":"string"},{"name":"Lot_Ageing","in":"body","type":"string"},{"name":"Process_Ageing","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"Pending_Lot_Qty","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/DcRouting/ZWM_GATE_ENTRY_RFCController.cs"},{"id":"zwmhustorettrfc","name":"ZWM_HU_STORE_TT_RFC","folder":"DcRouting","group":"DC Routing","rfc":"ZWM_HU_STORE_POST_RFC","route":"api/ZWM_HU_STORE_TT_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_EXIDV","in":"body","type":"string"},{"name":"IM_SAPHU","in":"body","type":"string"}],"tables":["LT_DATA","ES_RETURN"],"path":"Controllers/DcRouting/ZWM_HU_STORE_TT_RFCController.cs"},{"id":"zwmdcroutingrfc","name":"zwm_dc_routing_rfc","folder":"DcRouting","group":"DC Routing","rfc":"ZWM_DC_ROUTING_RFC","route":"api/zwm_dc_routing_rfc","method":"POST","params":[{"name":"IM_GATE_ENTRY","in":"body","type":"string"},{"name":"routingno","in":"body","type":"string"},{"name":"rout_desc","in":"body","type":"string"}],"tables":["LT_DATA","EX_RETURN"],"path":"Controllers/DcRouting/zwm_dc_routing_rfcController.cs"},{"id":"postfabputwaygrc","name":"Post_FABPUTWAYGRC","folder":"FMS_FABRIC_PUTWAY","group":"FMS Fabric Putway","rfc":"ZFMS_RFC_FABPUTWAYGRC_POST","route":"api/Post_FABPUTWAYGRC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_GRC","in":"body","type":"string"},{"name":"SITE","in":"body","type":"string"},{"name":"BIN","in":"body","type":"string"},{"name":"MATERIAL","in":"body","type":"string"},{"name":"SCAN_QTY","in":"body","type":"string"},{"name":"BATCH","in":"body","type":"string"},{"name":"GRC_NO","in":"body","type":"string"},{"name":"GR_LINE","in":"body","type":"string"},{"name":"ZWM_BIN_SCAN_T","in":"body","type":"string"}],"tables":["IT_DATA","ZWM_BIN_SCAN_T","ET_EAN_DATA","ET_DATA","EX_RETURN"],"path":"Controllers/FMS_FABRIC_PUTWAY/Post_FABPUTWAYGRCController.cs"},{"id":"validategrc","name":"Validate_GRC","folder":"FMS_FABRIC_PUTWAY","group":"FMS Fabric Putway","rfc":"ZFMS_RFC_FABPUTWAYGRC_VALIDATE","route":"api/Validate_GRC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_GRC","in":"body","type":"string"}],"tables":["ET_EAN_DATA","ET_DATA","EX_RETURN"],"path":"Controllers/FMS_FABRIC_PUTWAY/Validate_GRCController.cs"},{"id":"validationbin","name":"Validation_BIN","folder":"FMS_FABRIC_PUTWAY","group":"FMS Fabric Putway","rfc":"ZFMS_RFC_FABPUTWAYGRC_BIN_VALI","route":"api/Validation_BIN","method":"POST","params":[{"name":"IM_SITE","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"},{"name":"IM_LGTYP","in":"body","type":"string"}],"tables":["ET_EAN_DATA","ET_DATA","EX_RETURN"],"path":"Controllers/FMS_FABRIC_PUTWAY/Validation_BINController.cs"},{"id":"zadvancepaymentrfc","name":"ZADVANCE_PAYMENT_RFC","folder":"Finance","group":"Finance","rfc":"ZADVANCE_PAYMENT_RFC","route":"api/ZADVANCE_PAYMENT_RFC","method":"POST","params":[{"name":"I_COMPANY_CODE","in":"body","type":"string"},{"name":"I_POSTING_DATE_LOW","in":"body","type":"string"},{"name":"I_POSTING_DATE_HIGH","in":"body","type":"string"}],"tables":["IT_FINAL","EX_RETURN"],"path":"Controllers/Finance/ZADVANCE_PAYMENT_RFCController.cs"},{"id":"zrfccreditorslovabl","name":"ZRFC_CREDITORS_LOVABL","folder":"Finance","group":"Finance","rfc":"ZRFC_CREDITORS_LOVABL","route":"api/ZRFC_CREDITORS_LOVABL","method":"POST","params":[{"name":"COMPANY_CODE","in":"body","type":"string"},{"name":"VENDOR","in":"body","type":"string"},{"name":"POSTING_DATE","in":"body","type":"string"}],"tables":["LT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/Finance/ZRFC_CREDITORS_LOVABLController.cs"},{"id":"zsalesmoprfc","name":"ZSALES_MOP_RFC","folder":"Finance","group":"Finance","rfc":"ZSALES_MOP_RFC","route":"api/ZSALES_MOP_RFC","method":"POST","params":[{"name":"IM_DATE_LOW","in":"body","type":"string"},{"name":"IM_DATE_HIGH","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/Finance/ZSALES_MOP_RFCController.cs"},{"id":"gateboxvalidation","name":"Gate_Box_Validation","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_PALLATE1_N","route":"api/Gate_Box_Validation","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"},{"name":"IM_INV","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/Gate_Box_ValidationController.cs"},{"id":"gatevalidation","name":"Gate_Validation","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_VALIDATION1_N","route":"api/Gate_Validation","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/Gate_ValidationController.cs"},{"id":"pallateboxvalidation","name":"Pallate_Box_Validation","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_PALLATE1_N","route":"api/Pallate_Box_Validation","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"},{"name":"IM_INV","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/Pallate_Box_ValidationController.cs"},{"id":"zvndputwaybinvalrfc","name":"ZVND_PUTWAY_BIN_VAL_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZVND_PUTWAY_BIN_VAL_RFC","route":"api/ZVND_PUTWAY_BIN_VAL_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"}],"tables":["EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZVND_PUTWAY_BIN_VAL_RFCController.cs"},{"id":"zvndputwaypalettevalrfc","name":"ZVND_PUTWAY_PALETTE_VAL_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZVND_PUTWAY_PALETTE_VAL_RFC","route":"api/ZVND_PUTWAY_PALETTE_VAL_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZVND_PUTWAY_PALETTE_VAL_RFCController.cs"},{"id":"zwmgatebinvalidation3n","name":"ZWM_GATE_BIN_VALIDATION3_N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_BIN_VALIDATION3_N","route":"api/ZWM_GATE_BIN_VALIDATION3_N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_BIN_VALIDATION3_NController.cs"},{"id":"zwmgatebinvalidation4n","name":"ZWM_GATE_BIN_VALIDATION4_N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_BIN_VALIDATION4_N","route":"api/ZWM_GATE_BIN_VALIDATION4_N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"},{"name":"IM_GET","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_BIN_VALIDATION4_NController.cs"},{"id":"zwmgatebox3n","name":"ZWM_GATE_BOX3N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_BOX3N","route":"api/ZWM_GATE_BOX3N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_GATE","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"},{"name":"IM_BOX","in":"body","type":"string"},{"name":"IM_WEIGHT","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_BOX3NController.cs"},{"id":"zwmgatepallatevalidate3n","name":"ZWM_GATE_PALLATE_VALIDATE3_N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_PALLATE_VALIDATE3_N","route":"api/ZWM_GATE_PALLATE_VALIDATE3_N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_PALLATE_VALIDATE3_NController.cs"},{"id":"zwmgatepallatevalidate4n","name":"ZWM_GATE_PALLATE_VALIDATE4_N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_PALLATE_VALIDATE4_N","route":"api/ZWM_GATE_PALLATE_VALIDATE4_N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_BIN","in":"body","type":"string"},{"name":"IM_GET","in":"body","type":"string"},{"name":"IM_PALETTE","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_PALLATE_VALIDATE4_NController.cs"},{"id":"zwmgatesave3n","name":"ZWM_GATE_SAVE3_N","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GATE_SAVE3_N","route":"api/ZWM_GATE_SAVE3_N","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_GATE","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GATE_SAVE3_NController.cs"},{"id":"zwmgetgateentrydata4rfc","name":"ZWM_GET_GATE_ENTRY_DATA4_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GET_GATE_ENTRY_DATA4_RFC","route":"api/ZWM_GET_GATE_ENTRY_DATA4_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_GET","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GET_GATE_ENTRY_DATA4_RFCController.cs"},{"id":"zwmgetgateentrydatarfc","name":"ZWM_GET_GATE_ENTRY_DATA_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GET_GATE_ENTRY_DATA_RFC","route":"api/ZWM_GET_GATE_ENTRY_DATA_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_GATE","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GET_GATE_ENTRY_DATA_RFCController.cs"},{"id":"zwmgetgateentrylist4rfc","name":"ZWM_GET_GATE_ENTRY_LIST4_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GET_GATE_ENTRY_LIST4_RFC","route":"api/ZWM_GET_GATE_ENTRY_LIST4_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GET_GATE_ENTRY_LIST4_RFCController.cs"},{"id":"zwmgetgateentrylistrfc","name":"ZWM_GET_GATE_ENTRY_LIST_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GET_GATE_ENTRY_LIST_RFC","route":"api/ZWM_GET_GATE_ENTRY_LIST_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_DOCNO","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GET_GATE_ENTRY_LIST_RFCController.cs"},{"id":"zwmgetgateentrypallaterfc","name":"ZWM_GET_GATE_ENTRY_PALLATE_RFC","folder":"GateEntry_LOT_Putway","group":"Gate Entry / LOT Putaway","rfc":"ZWM_GET_GATE_ENTRY_PALLATE_RFC","route":"api/ZWM_GET_GATE_ENTRY_PALLATE_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_GATE","in":"body","type":"string"},{"name":"IM_PALL","in":"body","type":"string"}],"tables":["IT_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/GateEntry_LOT_Putway/ZWM_GET_GATE_ENTRY_PALLATE_RFCController.cs"},{"id":"zesicmasterpostrfc","name":"ZESIC_MASTER_POST_RFC","folder":"HRMS","group":"HRMS","rfc":"ZESIC_MASTER_POST_RFC","route":"api/ZESIC_MASTER_POST_RFC","method":"POST","params":[{"name":"IM_ST_CD","in":"body","type":"string"},{"name":"IM_STATUS","in":"body","type":"string"},{"name":"IM_ST_ESIC_CD","in":"body","type":"string"},{"name":"IM_ST_ESIC_CD_REF","in":"body","type":"string"},{"name":"StoreId","in":"body","type":"string"},{"name":"Vendor_Code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"PO_Number","in":"body","type":"string"},{"name":"Date","in":"body","type":"string"},{"name":"DeliveryDate","in":"body","type":"string"},{"name":"POQty","in":"body","type":"string"}],"tables":["LS_GET_DATA","ES_RETURN"],"path":"Controllers/HRMS/ZESIC_MASTER_POST_RFCController.cs"},{"id":"zhrleavepolicyrfc","name":"ZHR_LEAVE_POLICY_RFC","folder":"HRMS","group":"HRMS","rfc":"ZHR_LEAVE_POLICY_RFC","route":"api/ZHR_LEAVE_POLICY_RFC","method":"POST","params":[{"name":"PPT_NO","in":"body","type":"string"},{"name":"RTNO","in":"body","type":"string"},{"name":"START_AT_TIME","in":"body","type":"string"},{"name":"END_AT_TIME","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"},{"name":"PPT_ACCT","in":"body","type":"string"},{"name":"COMP_ART","in":"body","type":"string"}],"tables":["IM_DATA","ES_RETURN"],"path":"Controllers/HRMS/ZHR_LEAVE_POLICY_RFCController.cs"},{"id":"zlwfmasterpostrfc","name":"ZLWF_MASTER_POST_RFC","folder":"HRMS","group":"HRMS","rfc":"ZLWF_MASTER_POST_RFC","route":"api/ZLWF_MASTER_POST_RFC","method":"POST","params":[{"name":"ST_CD","in":"body","type":"string"},{"name":"APPLICABLE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"},{"name":"LWF_SITE_CODE_REF","in":"body","type":"string"},{"name":"TOTAL_AMOUNT","in":"body","type":"string"},{"name":"EMPLOYEE_TTL_CNTB","in":"body","type":"string"},{"name":"EMPLOYEE_MON_CNTB","in":"body","type":"string"},{"name":"EMPLOYER_TTL_CNTB","in":"body","type":"string"},{"name":"EMPLOYER_MON_CNTB","in":"body","type":"string"},{"name":"DEPOSITE_FREQ","in":"body","type":"string"},{"name":"StoreId","in":"body","type":"string"},{"name":"TAX_TYPE","in":"body","type":"string"},{"name":"FREQUENCY","in":"body","type":"string"},{"name":"MIN_WAGES_SLAB","in":"body","type":"string"},{"name":"MAX_WAGES_SLAB","in":"body","type":"string"},{"name":"Vendor_Code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"PO_Number","in":"body","type":"string"},{"name":"Date","in":"body","type":"string"},{"name":"DeliveryDate","in":"body","type":"string"},{"name":"POQty","in":"body","type":"string"}],"tables":["LS_GET_DATA","ES_RETURN"],"path":"Controllers/HRMS/ZLWF_MASTER_POST_RFCController.cs"},{"id":"zpfmasterpostrfc","name":"ZPF_MASTER_POST_RFC","folder":"HRMS","group":"HRMS","rfc":"ZPF_MASTER_POST_RFC","route":"api/ZPF_MASTER_POST_RFC","method":"POST","params":[{"name":"IM_ST_CD","in":"body","type":"string"},{"name":"IM_MIN_WAG","in":"body","type":"string"},{"name":"IM_STATUS","in":"body","type":"string"},{"name":"IM_ST_PF_CD","in":"body","type":"string"},{"name":"IM_ST_PF_CD_REF","in":"body","type":"string"},{"name":"StoreId","in":"body","type":"string"},{"name":"Vendor_Code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"PO_Number","in":"body","type":"string"},{"name":"Date","in":"body","type":"string"},{"name":"DeliveryDate","in":"body","type":"string"},{"name":"POQty","in":"body","type":"string"}],"tables":["LS_GET_DATA","ES_RETURN"],"path":"Controllers/HRMS/ZPF_MASTER_POST_RFCController.cs"},{"id":"zptmasterpostrfc","name":"ZPT_MASTER_POST_RFC","folder":"HRMS","group":"HRMS","rfc":"ZPT_MASTER_POST_RFC","route":"api/ZPT_MASTER_POST_RFC","method":"POST","params":[{"name":"ST_CD","in":"body","type":"string"},{"name":"APPLICABLE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"},{"name":"TAX_TYPE","in":"body","type":"string"},{"name":"LWF_SITE_CODE_REF","in":"body","type":"string"},{"name":"FREQUENCY","in":"body","type":"string"},{"name":"TOTAL_AMOUNT","in":"body","type":"string"},{"name":"EMPLOYEE_TTL_CNTB","in":"body","type":"string"},{"name":"EMPLOYEE_MON_CNTB","in":"body","type":"string"},{"name":"EMPLOYER_TTL_CNTB","in":"body","type":"string"},{"name":"EMPLOYER_MON_CNTB","in":"body","type":"string"},{"name":"MIN_WAGES_SLAB","in":"body","type":"string"},{"name":"MAX_WAGES_SLAB","in":"body","type":"string"},{"name":"StoreId","in":"body","type":"string"},{"name":"Vendor_Code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"PO_Number","in":"body","type":"string"},{"name":"Date","in":"body","type":"string"},{"name":"DeliveryDate","in":"body","type":"string"},{"name":"POQty","in":"body","type":"string"}],"tables":["LS_GET_DATA","ES_RETURN"],"path":"Controllers/HRMS/ZPT_MASTER_POST_RFCController.cs"},{"id":"articledata","name":"ArticleData","folder":"HU_Creation","group":"HU Creation","rfc":"ArticleData","route":"api/ArticleData","method":"POST","params":[],"tables":[],"path":"Controllers/HU_Creation/ArticleDataController.cs"},{"id":"barcode","name":"BarCode","folder":"HU_Creation","group":"HU Creation","rfc":"BarCode","route":"api/BarCode","method":"POST","params":[],"tables":[],"path":"Controllers/HU_Creation/BarCodeController.cs"},{"id":"hucreation","name":"HUCreation","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_PUSH_API_POST","route":"api/HUCreation","method":"POST","params":[{"name":"HU_NO","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"ARTICLE_NO","in":"body","type":"string"},{"name":"DESIGN","in":"body","type":"string"},{"name":"QUANTITY","in":"body","type":"string"},{"name":"VENDOR_CODE","in":"body","type":"string"},{"name":"EAN","in":"body","type":"string"},{"name":"CREATION_DATE","in":"body","type":"string"},{"name":"CREATION_TIME","in":"body","type":"string"},{"name":"CREATION_USER","in":"body","type":"string"},{"name":"MESSAGE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/HUCreationController.cs"},{"id":"hunamecheck","name":"HUNameCheck","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_CHECK_RFC","route":"api/HUNameCheck","method":"POST","params":[{"name":"HU_NO","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/HUNameCheckController.cs"},{"id":"hunamegenerator","name":"HUNameGenerator","folder":"HU_Creation","group":"HU Creation","rfc":"HUNameGenerator","route":"api/HUNameGenerator","method":"POST","params":[],"tables":[],"path":"Controllers/HU_Creation/HUNameGeneratorController.cs"},{"id":"poreport","name":"POReport","folder":"HU_Creation","group":"HU Creation","rfc":"POReport","route":"api/POReport","method":"POST","params":[],"tables":[],"path":"Controllers/HU_Creation/POReportController.cs"},{"id":"qrcodezip","name":"QrCodeZip","folder":"HU_Creation","group":"HU Creation","rfc":"QrCodeZip","route":"api/QrCodeZip","method":"POST","params":[],"tables":[],"path":"Controllers/HU_Creation/QrCodeZipController.cs"},{"id":"uploadarticledata","name":"UploadArticleData","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_PUSH_API_POST","route":"api/UploadArticleData","method":"POST","params":[{"name":"HU_NO","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"ARTICLE_NO","in":"body","type":"string"},{"name":"DESIGN","in":"body","type":"string"},{"name":"QUANTITY","in":"body","type":"string"},{"name":"VENDOR_CODE","in":"body","type":"string"},{"name":"EAN","in":"body","type":"string"},{"name":"CREATION_DATE","in":"body","type":"string"},{"name":"CREATION_TIME","in":"body","type":"string"},{"name":"CREATION_USER","in":"body","type":"string"},{"name":"MESSAGE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/UploadArticleDataController.cs"},{"id":"uploadpoarticledata","name":"UploadPoArticleData","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_PUSH_API_POST","route":"api/UploadPoArticleData","method":"POST","params":[{"name":"HU_NO","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"ARTICLE_NO","in":"body","type":"string"},{"name":"DESIGN","in":"body","type":"string"},{"name":"QUANTITY","in":"body","type":"string"},{"name":"VENDOR_CODE","in":"body","type":"string"},{"name":"EAN","in":"body","type":"string"},{"name":"CREATION_DATE","in":"body","type":"string"},{"name":"CREATION_TIME","in":"body","type":"string"},{"name":"CREATION_USER","in":"body","type":"string"},{"name":"MESSAGE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/UploadPoArticleDataController.cs"},{"id":"uservalidatepodetail","name":"UserValidatePODetail","folder":"HU_Creation","group":"HU Creation","rfc":"ZSRM_PO_DETAIL_FM","route":"api/UserValidatePODetail","method":"POST","params":[{"name":"PO_NUM","in":"body","type":"string"},{"name":"VENDOR_CODE","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/UserValidatePODetailController.cs"},{"id":"zvndhupushapipost","name":"ZVND_HU_PUSH_API_POST","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_PUSH_API_POST","route":"api/ZVND_HU_PUSH_API_POST","method":"POST","params":[{"name":"HU_NO","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"ARTICLE_NO","in":"body","type":"string"},{"name":"DESIGN","in":"body","type":"string"},{"name":"QUANTITY","in":"body","type":"string"},{"name":"VENDOR_CODE","in":"body","type":"string"},{"name":"EAN","in":"body","type":"string"},{"name":"CREATION_DATE","in":"body","type":"string"},{"name":"CREATION_TIME","in":"body","type":"string"},{"name":"CREATION_USER","in":"body","type":"string"},{"name":"MESSAGE","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/HU_Creation/ZVND_HU_PUSH_API_POSTController.cs"},{"id":"zvndhuvalidaterfc","name":"ZVND_HU_VALIDATE_RFC","folder":"HU_Creation","group":"HU Creation","rfc":"ZVND_HU_VALIDATE_RFC","route":"api/ZVND_HU_VALIDATE_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_HU_NUMBER","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_STORES","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/HU_Creation/ZVND_HU_VALIDATE_RFCController.cs"},{"id":"zwmvendpoheader","name":"ZWM_VEND_PO_HEADER","folder":"HU_Creation","group":"HU Creation","rfc":"ZWM_VEND_OPEN_PO","route":"api/ZWM_VEND_PO_HEADER","method":"POST","params":[{"name":"IM_LIFNR","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/HU_Creation/ZWM_VEND_PO_HEADERController.cs"},{"id":"huidentification","name":"HU_Identification","folder":"HU_PRINT","group":"HU Print","rfc":"ZWM_HU_STORE_RFC","route":"api/HU_Identification","method":"POST","params":[{"name":"IM_HU","in":"body","type":"string"},{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_EXIDV","in":"body","type":"string"},{"name":"SAP_HU","in":"body","type":"string"},{"name":"EXIDV","in":"body","type":"string"},{"name":"ST_CD","in":"body","type":"string"},{"name":"ST_NAME","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN","EX_RETURN"],"path":"Controllers/HU_PRINT/HU_IdentificationController.cs"},{"id":"huprint","name":"HU_Print","folder":"HU_PRINT","group":"HU Print","rfc":"ZWM_ACTUAL_HU_SAVE","route":"api/HU_Print","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_EXIDV","in":"body","type":"string"},{"name":"IM_SAP_HU","in":"body","type":"string"},{"name":"PO_Number","in":"body","type":"string"}],"tables":["EX_RETURN"],"path":"Controllers/HU_PRINT/HU_PrintController.cs"},{"id":"zwmsavehu","name":"ZWM_SAVE_HU","folder":"HU_SCAN","group":"HU Scan","rfc":"ZWM_SAVE_HU","route":"api/ZWM_SAVE_HU","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_HU","in":"body","type":"string"},{"name":"MATNR","in":"body","type":"string"},{"name":"HU_QTY","in":"body","type":"string"},{"name":"SCAN_QTY","in":"body","type":"string"},{"name":"DIFF_QTY","in":"body","type":"string"}],"tables":["IM_ARTICLES","ET_ERROR"],"path":"Controllers/HU_SCAN/ZWM_SAVE_HUController.cs"},{"id":"zwmscanhu","name":"ZWM_SCAN_HU","folder":"HU_SCAN","group":"HU Scan","rfc":"ZWM_SCAN_HU","route":"api/ZWM_SCAN_HU","method":"POST","params":[{"name":"IM_HU","in":"body","type":"string"},{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"}],"tables":["ET_ATICLES","ET_EAN","ET_ERROR"],"path":"Controllers/HU_SCAN/ZWM_SCAN_HUController.cs"},{"id":"nsoconfigpost","name":"NSOConfigPost","folder":"NSO","group":"NSO","rfc":"ZSRM_NSO_CONF_POST","route":"api/NSOConfigPost","method":"POST","params":[{"name":"IM_CSTATUS","in":"body","type":"string"},{"name":"IM_SRNO","in":"body","type":"string"},{"name":"IM_BUDGTE_ST_DATE","in":"body","type":"string"},{"name":"IM_BUDGTE_END_DATE","in":"body","type":"string"},{"name":"IM_ACT_START_DATE","in":"body","type":"string"},{"name":"IM_ACT_END_DATE","in":"body","type":"string"},{"name":"IM_START_AT_TIME","in":"body","type":"string"},{"name":"IM_END_AT_TIME","in":"body","type":"string"},{"name":"IM_REMARKS","in":"body","type":"string"},{"name":"IM_REMARKS1","in":"body","type":"string"},{"name":"IM_REMARKS2","in":"body","type":"string"},{"name":"IM_ROUTING_NO","in":"body","type":"string"},{"name":"IM_PROCESS_CONFIRM","in":"body","type":"string"},{"name":"IM_HYPERLINK","in":"body","type":"string"},{"name":"IM_BUDGET_ST_DATE","in":"body","type":"string"},{"name":"IM_BUDGET_END_DATE","in":"body","type":"string"},{"name":"IM_ACT_ST_DATE","in":"body","type":"string"},{"name":"IM_SRNO_Desc","in":"body","type":"string"},{"name":"IM_Rout_Desc","in":"body","type":"string"}],"tables":["ES_RETURN"],"path":"Controllers/NSO/NSOConfigPostController.cs"},{"id":"nsoconfigrouting","name":"NSOConfigRouting","folder":"NSO","group":"NSO","rfc":"ZSRM_NSO_CONF_ROUTING","route":"api/NSOConfigRouting","method":"POST","params":[{"name":"IM_SITE_CODE","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"MAJ_CAT","in":"body","type":"string"},{"name":"DESIGN_NO","in":"body","type":"string"},{"name":"QTY","in":"body","type":"string"},{"name":"RTNO","in":"body","type":"string"},{"name":"SiteCode","in":"body","type":"string"},{"name":"SITE_CODE","in":"body","type":"string"},{"name":"MANDT","in":"body","type":"string"},{"name":"SRNO","in":"body","type":"string"},{"name":"SPRAS","in":"body","type":"string"},{"name":"TEXT","in":"body","type":"string"}],"tables":["ET_DATA","EX_DATA","ES_RETURN"],"path":"Controllers/NSO/NSOConfigRoutingController.cs"},{"id":"nsoexpensepost","name":"NSOExpensePost","folder":"NSO","group":"NSO","rfc":"ZRFC_ACC_DOC_POST","route":"api/NSOExpensePost","method":"POST","params":[{"name":"IM_DC_ROUTING","in":"body","type":"string"},{"name":"IM_GATE_ENTRY","in":"body","type":"string"},{"name":"USERNAME","in":"body","type":"string"},{"name":"HEADER_TXT","in":"body","type":"string"},{"name":"DOC_DATE","in":"body","type":"string"},{"name":"PSTNG_DATE","in":"body","type":"string"},{"name":"TRANS_DATE","in":"body","type":"string"},{"name":"FISC_YEAR","in":"body","type":"string"},{"name":"FIS_PERIOD","in":"body","type":"string"},{"name":"REF_DOC_NO","in":"body","type":"string"},{"name":"ITEMNO_ACC","in":"body","type":"string"},{"name":"GL_ACCOUNT","in":"body","type":"string"},{"name":"COSTCENTER","in":"body","type":"string"},{"name":"PROFIT_CTR","in":"body","type":"string"},{"name":"AMT_DOCCUR","in":"body","type":"string"},{"name":"VENDOR_NO","in":"body","type":"string"},{"name":"BLINE_DATE","in":"body","type":"string"},{"name":"ITEM_TEXT","in":"body","type":"string"},{"name":"GE_NO","in":"body","type":"string"},{"name":"DCNO","in":"body","type":"string"},{"name":"TEXT","in":"body","type":"string"},{"name":"EMP_CD","in":"body","type":"string"},{"name":"PO_NUM","in":"body","type":"string"}],"tables":["LT_ACCOUNTGL","LT_CURRENCYAMOUNT","LT_PAYBLE","LT_DATA","LS_HEADER","ES_RETURN"],"path":"Controllers/NSO/NSOExpensePostController.cs"},{"id":"nsositelist","name":"NSOSiteList","folder":"NSO","group":"NSO","rfc":"ZNSO_RFC_SITELIST","route":"api/NSOSiteList","method":"POST","params":[{"name":"IM_SITE_CODE","in":"body","type":"string"},{"name":"PO_NO","in":"body","type":"string"},{"name":"MAJ_CAT","in":"body","type":"string"},{"name":"DESIGN_NO","in":"body","type":"string"},{"name":"QTY","in":"body","type":"string"},{"name":"RTNO","in":"body","type":"string"},{"name":"SITE_CODE","in":"body","type":"string"},{"name":"WERKS","in":"body","type":"string"},{"name":"KTEXT","in":"body","type":"string"},{"name":"ZSNRO","in":"body","type":"string"}],"tables":["ET_DATA","EX_DATA","ES_RETURN"],"path":"Controllers/NSO/NSOSiteListController.cs"},{"id":"nsoglcodes","name":"NSO_GL_CODES","folder":"NSO","group":"NSO","rfc":"ZRFC_GL_CODE","route":"api/NSO_GL_CODES","method":"POST","params":[{"name":"IM_EBELN","in":"body","type":"string"},{"name":"MANDT","in":"body","type":"string"},{"name":"GL_ACC","in":"body","type":"string"},{"name":"GL_TYPE","in":"body","type":"string"},{"name":"ZDESC","in":"body","type":"string"}],"tables":["GT_DATA","EX_RETURN"],"path":"Controllers/NSO/NSO_GL_CODESController.cs"},{"id":"getpicklistdata","name":"GetPicklistData","folder":"PaperlessPicklist","group":"Paperless Picklist","rfc":"ZWM_RFC_GET_PICKLIST_DATA","route":"api/GetPicklistData","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PICNR","in":"body","type":"string"}],"tables":["ET_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/PaperlessPicklist/GetPicklistDataController.cs"},{"id":"getpicklistnumber","name":"GetPicklistNumber","folder":"PaperlessPicklist","group":"Paperless Picklist","rfc":"ZWM_RFC_GET_PICKLIST","route":"api/GetPicklistNumber","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_DATUM","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"WM_NO","in":"body","type":"string"},{"name":"MATERIAL","in":"body","type":"string"},{"name":"PLANT","in":"body","type":"string"},{"name":"STOR_LOC","in":"body","type":"string"},{"name":"BATCH","in":"body","type":"string"},{"name":"CRATE","in":"body","type":"string"},{"name":"BIN","in":"body","type":"string"},{"name":"STORAGE_TYPE","in":"body","type":"string"},{"name":"MEINS","in":"body","type":"string"},{"name":"AVL_STOCK","in":"body","type":"string"},{"name":"OPEN_STOCK","in":"body","type":"string"},{"name":"SCAN_QTY","in":"body","type":"string"},{"name":"PICNR","in":"body","type":"string"},{"name":"PICK_QTY","in":"body","type":"string"},{"name":"HU_NO","in":"body","type":"string"},{"name":"BARCODE","in":"body","type":"string"},{"name":"MATKL","in":"body","type":"string"},{"name":"WGBEZ","in":"body","type":"string"},{"name":"SONUM","in":"body","type":"string"},{"name":"DELNUM","in":"body","type":"string"},{"name":"POSNR","in":"body","type":"string"},{"name":"GNATURE","in":"body","type":"string"},{"name":"SAMMG","in":"body","type":"string"},{"name":"PICK_STATUS","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/PaperlessPicklist/GetPicklistNumberController.cs"},{"id":"postpicklistscanneddata","name":"PostPickListScannedData","folder":"PaperlessPicklist","group":"Paperless Picklist","rfc":"ZWM_RFC_PICKLIST_SCAN_POST","route":"api/PostPickListScannedData","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_WERKS","in":"body","type":"string"},{"name":"IM_PICNR","in":"body","type":"string"},{"name":"WM_NO","in":"body","type":"string"},{"name":"MATERIAL","in":"body","type":"string"},{"name":"PLANT","in":"body","type":"string"},{"name":"STOR_LOC","in":"body","type":"string"},{"name":"BATCH","in":"body","type":"string"},{"name":"CRATE","in":"body","type":"string"},{"name":"BIN","in":"body","type":"string"},{"name":"STORAGE_TYPE","in":"body","type":"string"},{"name":"MEINS","in":"body","type":"string"},{"name":"AVL_STOCK","in":"body","type":"string"},{"name":"OPEN_STOCK","in":"body","type":"string"},{"name":"SCAN_QTY","in":"body","type":"string"},{"name":"PICNR","in":"body","type":"string"},{"name":"PICK_QTY","in":"body","type":"string"},{"name":"HU_NO","in":"body","type":"string"},{"name":"BARCODE","in":"body","type":"string"},{"name":"MATKL","in":"body","type":"string"},{"name":"WGBEZ","in":"body","type":"string"},{"name":"SONUM","in":"body","type":"string"},{"name":"DELNUM","in":"body","type":"string"},{"name":"POSNR","in":"body","type":"string"},{"name":"GNATURE","in":"body","type":"string"},{"name":"SAMMG","in":"body","type":"string"},{"name":"PICK_STATUS","in":"body","type":"string"}],"tables":["IT_DATA","ET_DATA","EX_RETURN"],"path":"Controllers/PaperlessPicklist/PostPickListScannedDataController.cs"},{"id":"articleidentifier","name":"ArticleIdentifier","folder":"Sampling","group":"Sampling","rfc":"ZEAN_ART_DETAILS","route":"api/ArticleIdentifier","method":"POST","params":[{"name":"LV_ART","in":"body","type":"string"},{"name":"HU_NO","in":"body","type":"string"},{"name":"Article_Number","in":"body","type":"string"},{"name":"Vendor_Code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"GRC_Date","in":"body","type":"string"},{"name":"GRC_Cost","in":"body","type":"string"},{"name":"GRC_Qty","in":"body","type":"string"},{"name":"Bill_No","in":"body","type":"string"}],"tables":["ET_DATA","EX_DATA","ES_RETURN"],"path":"Controllers/Sampling/ArticleIdentifierController.cs"},{"id":"articleyesorno","name":"ArticleYesOrNo","folder":"Sampling","group":"Sampling","rfc":"ZARTICLE_YES_NO_POST","route":"api/ArticleYesOrNo","method":"POST","params":[{"name":"ID","in":"body","type":"string"},{"name":"ARTICLE","in":"body","type":"string"},{"name":"CREATION_DT","in":"body","type":"string"},{"name":"CREATION_TM","in":"body","type":"string"},{"name":"STATUS","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"}],"tables":["EX_RETURN"],"path":"Controllers/Sampling/ArticleYesOrNoController.cs"},{"id":"articleyesornodata","name":"ArticleYesOrNoData","folder":"Sampling","group":"Sampling","rfc":"ArticleYesOrNoData","route":"api/ArticleYesOrNoData","method":"POST","params":[],"tables":[],"path":"Controllers/Sampling/ArticleYesOrNoDataController.cs"},{"id":"qcdone","name":"QcDone","folder":"Sampling","group":"Sampling","rfc":"ZQCDONE_RFC","route":"api/QcDone","method":"POST","params":[{"name":"LV_ART","in":"body","type":"string"},{"name":"HU_NO","in":"body","type":"string"}],"tables":["ET_DATA","EX_DATA","ES_RETURN"],"path":"Controllers/Sampling/QcDoneController.cs"},{"id":"storesiteconfsave","name":"Store_Site_CONF_SAVE","folder":"Site_Creation","group":"Site Creation","rfc":"ZWM_STORE_SITE_CONF_SAVE","route":"api/Store_Site_CONF_SAVE","method":"POST","params":[{"name":"SRNO","in":"body","type":"string"},{"name":"PROC_DESC","in":"body","type":"string"},{"name":"PROC_CONF","in":"body","type":"string"},{"name":"ACT_START_DATE","in":"body","type":"string"},{"name":"ACT_END_DATE","in":"body","type":"string"},{"name":"REMARK","in":"body","type":"string"}],"tables":["IM_DATA","EX_RETURN"],"path":"Controllers/Site_Creation/Store_Site_CONF_SAVEController.cs"},{"id":"storesitecreation","name":"Store_Site_Creation","folder":"Site_Creation","group":"Site Creation","rfc":"ZSITE_RFC_CREATE","route":"api/Store_Site_Creation","method":"POST","params":[{"name":"RM_NAME","in":"body","type":"string"},{"name":"ZONE_1","in":"body","type":"string"},{"name":"ZSTATE","in":"body","type":"string"},{"name":"DISTRICT_NAME","in":"body","type":"string"},{"name":"CITY","in":"body","type":"string"},{"name":"CITY_POPULATION","in":"body","type":"string"},{"name":"C_B_PASS","in":"body","type":"string"},{"name":"B_PASS","in":"body","type":"string"},{"name":"F_PASS","in":"body","type":"string"},{"name":"LLRATE","in":"body","type":"string"},{"name":"VRATE","in":"body","type":"string"},{"name":"RANK","in":"body","type":"string"},{"name":"SITE_TYPE","in":"body","type":"string"},{"name":"MRKT_NAME","in":"body","type":"string"},{"name":"FRONTAGE","in":"body","type":"string"},{"name":"TOTAL_AREA","in":"body","type":"string"},{"name":"BSMT_PRKG","in":"body","type":"string"},{"name":"FRONT_PRKG","in":"body","type":"string"},{"name":"UGF","in":"body","type":"string"},{"name":"LGF","in":"body","type":"string"},{"name":"GROUND_FLOOR","in":"body","type":"string"},{"name":"FIRST_FLOOR","in":"body","type":"string"},{"name":"SECOND_FLOOR","in":"body","type":"string"},{"name":"THIRD_FLOOR","in":"body","type":"string"},{"name":"FORTH_FLOOR","in":"body","type":"string"},{"name":"FIFTH_FLOOR","in":"body","type":"string"},{"name":"GOOGLE_COORDINATES","in":"body","type":"string"},{"name":"COMPETITORS_NAME","in":"body","type":"string"},{"name":"COMPETITORS_SALE","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"},{"name":"REMARKS1","in":"body","type":"string"},{"name":"REMARKS2","in":"body","type":"string"},{"name":"BROKER_NAME","in":"body","type":"string"},{"name":"BROKERM_NO","in":"body","type":"string"},{"name":"LANDLORD_NAME","in":"body","type":"string"},{"name":"LANDLORD_M_NO","in":"body","type":"string"},{"name":"PROOF","in":"body","type":"string"},{"name":"DISTRICT_POPULATION","in":"body","type":"string"},{"name":"CITY_POPULATION2","in":"body","type":"string"},{"name":"DISTT_POP_PER_KM","in":"body","type":"string"},{"name":"CITY_POP_PER_KM","in":"body","type":"string"},{"name":"LITERACY_RATE","in":"body","type":"string"},{"name":"NO_OF_SCH_10_KM","in":"body","type":"string"},{"name":"NO_OF_COL_UN_10","in":"body","type":"string"},{"name":"AV_HLD_INC_DIST","in":"body","type":"string"},{"name":"NO_ATM_CITY","in":"body","type":"string"},{"name":"NO_OF_BANK_CITY","in":"body","type":"string"},{"name":"NO_IND_FAC","in":"body","type":"string"},{"name":"UNEMP_RATE_CITY","in":"body","type":"string"},{"name":"DIS_FR_RAIL","in":"body","type":"string"},{"name":"DIS_FR_BUS","in":"body","type":"string"},{"name":"NO4_WH_PASS","in":"body","type":"string"},{"name":"NO2_WH_PASS","in":"body","type":"string"},{"name":"NO_SHOP_MALL","in":"body","type":"string"},{"name":"NO_MUTI_CIN_CITY","in":"body","type":"string"},{"name":"PRES_FOOD_COURT","in":"body","type":"string"}],"tables":["IM_DATA","ES_RETURN"],"path":"Controllers/Site_Creation/Store_Site_CreationController.cs"},{"id":"storesitelist","name":"Store_Site_List","folder":"Site_Creation","group":"Site Creation","rfc":"ZWM_STORE_SITE_CONF","route":"api/Store_Site_List","method":"POST","params":[{"name":"IM_SITE_CODE","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/Site_Creation/Store_Site_ListController.cs"},{"id":"zwmhubwisestorelistrfc","name":"ZWM_HUBWISE_STORE_LIST_RFC","folder":"Vehicle_Loading","group":"Vehicle Loading","rfc":"ZWM_HUBWISE_STORE_LIST_RFC","route":"api/ZWM_HUBWISE_STORE_LIST_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_HUB","in":"body","type":"string"}],"tables":["ET_STORES","ET_EAN_DATA","ET_ERROR"],"path":"Controllers/Vehicle_Loading/ZWM_HUBWISE_STORE_LIST_RFCController.cs"},{"id":"zwmhuselectionrfc","name":"ZWM_HU_SELECTION_RFC","folder":"Vehicle_Loading","group":"Vehicle Loading","rfc":"ZWM_HU_SELECTION_RFC","route":"api/ZWM_HU_SELECTION_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_VEH","in":"body","type":"string"},{"name":"IM_TRANSPORT_CODE","in":"body","type":"string"},{"name":"IM_SEAL_NO","in":"body","type":"string"},{"name":"IM_DRIVER_NAME","in":"body","type":"string"},{"name":"IM_DRIVER_MOB","in":"body","type":"string"},{"name":"IM_HUB_FLAG","in":"body","type":"string"},{"name":"IM_STORE_FLAG","in":"body","type":"string"},{"name":"IM_HUB","in":"body","type":"string"},{"name":"IM_GRP","in":"body","type":"string"},{"name":"STORE","in":"body","type":"string"}],"tables":["STORE_LIST","ET_HULIST","ET_EAN_DATA","ET_ERROR"],"path":"Controllers/Vehicle_Loading/ZWM_HU_SELECTION_RFCController.cs"},{"id":"zwmsavescannedhulistrfc","name":"ZWM_SAVE_SCANNEDHULIST_RFC","folder":"Vehicle_Loading","group":"Vehicle Loading","rfc":"ZWM_SAVE_SCANNEDHULIST_RFC","route":"api/ZWM_SAVE_SCANNEDHULIST_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_VEHICLE","in":"body","type":"string"},{"name":"HU_LIST","in":"body","type":"string"},{"name":"IM_REMOVE","in":"body","type":"string"},{"name":"SRC_STORE","in":"body","type":"string"},{"name":"DST_STORE","in":"body","type":"string"},{"name":"LRNO","in":"body","type":"string"},{"name":"EXTERNAL_HU","in":"body","type":"string"},{"name":"INTERNAL_HU","in":"body","type":"string"},{"name":"QUANTITY","in":"body","type":"string"},{"name":"PALETTE","in":"body","type":"string"},{"name":"CLA_BIN","in":"body","type":"string"},{"name":"DCLA_STATUS","in":"body","type":"string"},{"name":"UOM","in":"body","type":"string"},{"name":"SCAN","in":"body","type":"string"}],"tables":["HU_LIST","ET_HULIST","ET_EAN_DATA","ET_ERROR"],"path":"Controllers/Vehicle_Loading/ZWM_SAVE_SCANNEDHULIST_RFCController.cs"},{"id":"zwmtransporterdetailsrfc","name":"ZWM_TRANSPORTER_DETAILS_RFC","folder":"Vehicle_Loading","group":"Vehicle Loading","rfc":"ZWM_TRANSPORTER_DETAILS_RFC","route":"api/ZWM_TRANSPORTER_DETAILS_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PLANT","in":"body","type":"string"},{"name":"IM_HUB","in":"body","type":"string"}],"tables":["ET_TRANSPORT_DET","ET_EAN_DATA","ET_ERROR"],"path":"Controllers/Vehicle_Loading/ZWM_TRANSPORTER_DETAILS_RFCController.cs"},{"id":"completeroutingstatuslist","name":"CompleteRoutingStatusList","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_GET_ROUTING_LIST","route":"api/CompleteRoutingStatusList","method":"POST","params":[{"name":"IM_PO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/CompleteRoutingStatusListController.cs"},{"id":"majdrp","name":"Maj_Drp","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZPUR_TREND_F4","route":"api/Maj_Drp","method":"POST","params":[{"name":"I_GJAHR","in":"body","type":"string"},{"name":"I_MATCAT","in":"body","type":"string"}],"tables":["ET_CAT2","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/Maj_DrpController.cs"},{"id":"podeldatedatapost","name":"PO_DEL_Date_DATA_POST","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_RFC_PO_UPDATE_DELV_DATE","route":"api/PO_DEL_Date_DATA_POST","method":"POST","params":[{"name":"IM_PO_NUMBER","in":"body","type":"string"},{"name":"IM_DELIVERY_DATE","in":"body","type":"string"}],"tables":["IT_DATA","ZST_SRM_ASN","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PO_DEL_Date_DATA_POSTController.cs"},{"id":"podetail","name":"PO_Detail","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_PO_DETAIL","route":"api/PO_Detail","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PO_DetailController.cs"},{"id":"pofabpo","name":"PO_Fab_PO","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_RTL_FAB_PO","route":"api/PO_Fab_PO","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_EBELN","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PO_Fab_POController.cs"},{"id":"pptroutingstatuslist","name":"PPTRoutingStatusList","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZRFC_PPT_GET_ROUT","route":"api/PPTRoutingStatusList","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"},{"name":"IM_PPT_NO","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PPTRoutingStatusListController.cs"},{"id":"pptlist","name":"PPT_LIST","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZRFC_PPT_GET","route":"api/PPT_LIST","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"},{"name":"PPT_NO","in":"body","type":"string"},{"name":"Vendor_code","in":"body","type":"string"},{"name":"Vendor_Name","in":"body","type":"string"},{"name":"PPT_Creation_date","in":"body","type":"string"},{"name":"Subdivision","in":"body","type":"string"},{"name":"LRTNO_desc","in":"body","type":"string"},{"name":"LRTNO","in":"body","type":"string"},{"name":"PPT_SHOW","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PPT_LISTController.cs"},{"id":"pptpost","name":"PPT_POST","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZRFC_PPT_CONF_POST","route":"api/PPT_POST","method":"POST","params":[{"name":"PPT_NO","in":"body","type":"string"},{"name":"RTNO","in":"body","type":"string"},{"name":"START_AT_TIME","in":"body","type":"string"},{"name":"END_AT_TIME","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"},{"name":"PPT_ACCT","in":"body","type":"string"},{"name":"COMP_ART","in":"body","type":"string"}],"tables":["IM_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PPT_POSTController.cs"},{"id":"prvspo","name":"PRvsPO","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZPUR_TREND_DATA","route":"api/PRvsPO","method":"POST","params":[{"name":"I_GJAHR","in":"body","type":"string"},{"name":"I_MATCAT","in":"body","type":"string"}],"tables":["ET_DAT","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PRvsPOController.cs"},{"id":"paymentsrm","name":"Paymentsrm","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_VEND_PAYMENT_INFO","route":"api/Paymentsrm","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_LIFNR","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/PaymentsrmController.cs"},{"id":"routingstatusgroup","name":"RoutingStatusGROUP","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_PO_RFC_GET_ROUTING","route":"api/RoutingStatusGROUP","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_GROUT_STAT","ET_PRD_ROUTING","ET_BR","ET_ACCST","ET_PPSR","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/RoutingStatusGROUPController.cs"},{"id":"routingstatuslist","name":"RoutingStatusList","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_PO_RFC_GET_ROUTING","route":"api/RoutingStatusList","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/RoutingStatusListController.cs"},{"id":"routingstatusgrplist","name":"RoutingStatusgrpList","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_PO_RFC_GET_ROUTING","route":"api/RoutingStatusgrpList","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_DESIGN","in":"body","type":"string"},{"name":"IM_SATNR","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_DATA","ET_PRD_ROUTING","ET_BR","ET_ACCST","ET_PPSR","ET_AMS","ET_FTA","ET_POSR","ET_TPM","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/RoutingStatusgrpListController.cs"},{"id":"updateroutingstatus","name":"Update_Routing_Status","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZSRM_ROUTING_POST","route":"api/Update_Routing_Status","method":"POST","params":[{"name":"PO_NO","in":"body","type":"string"},{"name":"MAJ_CAT","in":"body","type":"string"},{"name":"DESIGN_NO","in":"body","type":"string"},{"name":"QTY","in":"body","type":"string"},{"name":"RTNO","in":"body","type":"string"},{"name":"COMP_ART","in":"body","type":"string"},{"name":"FILEPATH","in":"body","type":"string"},{"name":"REMARKS","in":"body","type":"string"},{"name":"HHTUSER","in":"body","type":"string"},{"name":"ASN_NO","in":"body","type":"string"},{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["EX_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Routing/Update_Routing_StatusController.cs"},{"id":"zme2mlive","name":"ZME2M_LIve","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZME2M_LIVE","route":"api/ZME2M_LIve","method":"POST","params":[{"name":"IM_DATE_FROM","in":"body","type":"string"},{"name":"IM_DATE_TO","in":"body","type":"string"},{"name":"IM_COMP","in":"body","type":"string"},{"name":"SIGN","in":"body","type":"string"},{"name":"OPTION","in":"body","type":"string"},{"name":"LOW","in":"body","type":"string"},{"name":"HIGH","in":"body","type":"string"},{"name":"Stcode","in":"body","type":"string"}],"tables":["IT_WERKS","ET_ARTICLE_COLOR","ET_PUR_DATA","ET_EAN_DATA","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/ZME2M_LIveController.cs"},{"id":"zmmartcreationrfc","name":"ZMM_ART_CREATION_RFC","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZMM_ART_CREATION_RFC","route":"api/ZMM_ART_CREATION_RFC","method":"POST","params":[{"name":"HSN_CODE","in":"body","type":"string"},{"name":"SUB_DIV","in":"body","type":"string"},{"name":"MC_CD","in":"body","type":"string"},{"name":"VENDOR","in":"body","type":"string"},{"name":"DSG_NO","in":"body","type":"string"},{"name":"MRP","in":"body","type":"string"},{"name":"SEASON","in":"body","type":"string"},{"name":"ARTICLE_DES1","in":"body","type":"string"},{"name":"PRICE_BAND_CATEGORY","in":"body","type":"string"},{"name":"M_MAIN_MVGR","in":"body","type":"string"},{"name":"M_MACRO_MVGR","in":"body","type":"string"},{"name":"M_FAB_DIV","in":"body","type":"string"},{"name":"M_FAB","in":"body","type":"string"},{"name":"M_FAB2","in":"body","type":"string"},{"name":"M_YARN","in":"body","type":"string"},{"name":"M_YARN02","in":"body","type":"string"},{"name":"M_WEAVE_2","in":"body","type":"string"},{"name":"M_COMPOSITION","in":"body","type":"string"},{"name":"M_FINISH","in":"body","type":"string"},{"name":"M_CONSTRUCTION","in":"body","type":"string"},{"name":"M_SHADE","in":"body","type":"string"},{"name":"M_LYCRA","in":"body","type":"string"},{"name":"M_GSM","in":"body","type":"string"},{"name":"M_COUNT","in":"body","type":"string"},{"name":"M_OUNZ","in":"body","type":"string"},{"name":"M_COLLAR","in":"body","type":"string"},{"name":"M_NECK_BAND_STYLE","in":"body","type":"string"},{"name":"M_PLACKET","in":"body","type":"string"},{"name":"M_BLT_MAIN_STYLE","in":"body","type":"string"},{"name":"M_SUB_STYLE_BLT","in":"body","type":"string"},{"name":"M_SLEEVES_MAIN_STYLE","in":"body","type":"string"},{"name":"M_BTM_FOLD","in":"body","type":"string"},{"name":"M_NECK_BAND","in":"body","type":"string"},{"name":"M_FO_BTN_STYLE","in":"body","type":"string"},{"name":"NO_OF_POCKET","in":"body","type":"string"},{"name":"M_POCKET","in":"body","type":"string"},{"name":"POCKET_PLACEMENT","in":"body","type":"string"},{"name":"M_FIT","in":"body","type":"string"},{"name":"M_PATTERN","in":"body","type":"string"},{"name":"M_LENGTH","in":"body","type":"string"},{"name":"M_DC_SUB_STYLE","in":"body","type":"string"},{"name":"M_BTN_MAIN_MVGR","in":"body","type":"string"},{"name":"M_ZIP","in":"body","type":"string"},{"name":"M_ZIP_COL","in":"body","type":"string"},{"name":"M_PRINT_TYPE","in":"body","type":"string"},{"name":"M_PRINT_PLACEMENT","in":"body","type":"string"},{"name":"M_PRINT_STYLE","in":"body","type":"string"},{"name":"M_PATCHES","in":"body","type":"string"},{"name":"M_PATCH_TYPE","in":"body","type":"string"},{"name":"M_EMBROIDERY","in":"body","type":"string"},{"name":"M_EMB_TYPE","in":"body","type":"string"},{"name":"M_WASH","in":"body","type":"string"},{"name":"M_PD","in":"body","type":"string"},{"name":"MVGR_BRAND_VENDOR","in":"body","type":"string"},{"name":"M_YARN_02","in":"body","type":"string"}],"tables":["IM_DATA","LT_DATA","ET_EAN_DATA","EX_DATA"],"path":"Controllers/Vendor_SRM_Routing/ZMM_ART_CREATION_RFCController.cs"},{"id":"zpomodification","name":"ZPO_MODIFICATION","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZPO_MODIFICATION","route":"api/ZPO_MODIFICATION","method":"POST","params":[{"name":"IM_PO_NO","in":"body","type":"string"},{"name":"IM_PO_DEL_DATE","in":"body","type":"string"},{"name":"IM_DEL_CHG_DATE_LOW","in":"body","type":"string"},{"name":"IM_DEL_CHG_DATE_HIGH","in":"body","type":"string"},{"name":"EBELN","in":"body","type":"string"},{"name":"ORIGNAL_DEL_DATE","in":"body","type":"string"},{"name":"CHNG_NO","in":"body","type":"string"},{"name":"CURRENT_DEL_DATE","in":"body","type":"string"},{"name":"DEL_EXT_DATE","in":"body","type":"string"},{"name":"DELAYED_BY","in":"body","type":"string"},{"name":"REASON","in":"body","type":"string"}],"tables":["ET_PO_OUTPUT","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/ZPO_MODIFICATIONController.cs"},{"id":"zvndputwaysavedatarfc","name":"ZVND_PUTWAY_SAVE_DATA_RFC","folder":"Vendor_SRM_Routing","group":"Vendor SRM / Routing","rfc":"ZVND_PUTWAY_SAVE_DATA_RFC","route":"api/ZVND_PUTWAY_SAVE_DATA_RFC","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"}],"tables":["IT_DATA","EX_RETURN"],"path":"Controllers/Vendor_SRM_Routing/ZVND_PUTWAY_SAVE_DATA_RFCController.cs"},{"id":"zonevendordata","name":"ZoneVendorData","folder":"Vendor_SRM_Zone","group":"Vendor SRM Zone","rfc":"ZSRM_GET_VENDOR_ZONE_DATA","route":"api/ZoneVendorData","method":"POST","params":[{"name":"IM_ZONE_ID","in":"body","type":"string"}],"tables":["ET_DATA","EX_RETURN"],"path":"Controllers/Vendor_SRM_Zone/ZoneVendorDataController.cs"},{"id":"zonevendorpodata","name":"ZoneVendorPoData","folder":"Vendor_SRM_Zone","group":"Vendor SRM Zone","rfc":"ZSRM_VEND_PEND_PO","route":"api/ZoneVendorPoData","method":"POST","params":[{"name":"IM_LIFNR","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Zone/ZoneVendorPoDataController.cs"},{"id":"zonepodetail","name":"Zone_PO_Detail","folder":"Vendor_SRM_Zone","group":"Vendor SRM Zone","rfc":"ZSRM_PO_DETAIL","route":"api/Zone_PO_Detail","method":"POST","params":[{"name":"IM_USER","in":"body","type":"string"},{"name":"IM_PO","in":"body","type":"string"}],"tables":["ET_DATA","ES_RETURN"],"path":"Controllers/Vendor_SRM_Zone/Zone_PO_DetailController.cs"}];

const GITHUB = "https://github.com/akash0631/rfc-api/blob/master/";
let current = null, colOps = [];

function init() {
  const groups = [...new Set(ENDPOINTS.map(e=>e.group))];
  const totalP = ENDPOINTS.reduce((s,e)=>s+e.params.length,0);
  const totalT = new Set(ENDPOINTS.flatMap(e=>e.tables)).size;
  document.getElementById("tc").textContent = ENDPOINTS.length;
  document.getElementById("gc").textContent = groups.length;
  document.getElementById("ws1").textContent = ENDPOINTS.length;
  document.getElementById("ws2").textContent = groups.length;
  document.getElementById("ws3").textContent = totalP;
  document.getElementById("ws4").textContent = totalT;
  renderSidebar(ENDPOINTS);
  if (location.hash) {
    const ep = ENDPOINTS.find(e=>e.id===location.hash.slice(1));
    if (ep) show(ep);
  }
}

function renderSidebar(eps) {
  const grouped = {};
  eps.forEach(e=>{ if(!grouped[e.group]) grouped[e.group]=[]; grouped[e.group].push(e); });
  const el = document.getElementById("sidebarList");
  el.innerHTML = Object.entries(grouped).map(([g,list])=>\`
    <div class="group-header" onclick="tog('g_\${g.replace(/\\W/g,'_')}')">
      <span class="group-label">\${g}</span>
      <span class="group-count">\${list.length}</span>
    </div>
    <div id="g_\${g.replace(/\\W/g,'_')}" class="group-endpoints">
      \${list.map(e=>\`
        <div class="ep-item" id="i_\${e.id}" onclick="show(ENDPOINTS.find(x=>x.id==='\${e.id}'))">
          <span class="ep-dot"></span>\${e.name}
        </div>\`).join("")}
    </div>
  \`).join("");
}

function tog(id){ const el=document.getElementById(id); if(el) el.style.display=el.style.display==="none"?"":"none"; }

function doSearch() {
  const q = document.getElementById("srch").value.toLowerCase().trim();
  renderSidebar(q ? ENDPOINTS.filter(e=>
    e.name.toLowerCase().includes(q)||e.rfc.toLowerCase().includes(q)||
    e.group.toLowerCase().includes(q)||e.params.some(p=>p.name.toLowerCase().includes(q))||
    e.tables.some(t=>t.toLowerCase().includes(q))
  ) : ENDPOINTS);
}

function show(ep) {
  if (!ep) return;
  current = ep; colOps = [];
  location.hash = ep.id;
  document.querySelectorAll(".ep-item").forEach(el=>el.classList.remove("active"));
  const item = document.getElementById("i_"+ep.id);
  if (item) { item.classList.add("active"); item.scrollIntoView({block:"nearest"}); }
  document.getElementById("welcome").style.display = "none";
  document.getElementById("detail").style.display = "block";

  const paramsHtml = ep.params.length ? \`
    <table class="param-table">
      <thead><tr><th>Parameter</th><th>Type</th><th>Source</th></tr></thead>
      <tbody>\${ep.params.map(p=>\`
        <tr><td class="pname">\${p.name}</td><td class="ptype">string</td><td><span class="pbadge">body</span></td></tr>
      \`).join("")}</tbody>
    </table>\` : \`<div class="no-data">No parameters extracted — view source file for details.</div>\`;

  const tablesHtml = ep.tables.length
    ? ep.tables.map(t=>\`<span class="chip">\${t}</span>\`).join("")
    : \`<span style="color:var(--dim);font-size:12px;padding:0 4px;">No tables detected</span>\`;

  document.getElementById("detail").innerHTML = \`
    <div class="ep-header">
      <div class="ep-title-row">
        <span class="method-badge">POST</span>
        <span class="ep-name">\${ep.name}</span>
      </div>
      <div class="ep-meta">SAP RFC: <span>\${ep.rfc}</span> &nbsp;·&nbsp; Module: <span>\${ep.group}</span></div>
      <div class="ep-route-box">
        <span class="ep-route-base">IIS → SAP /</span><span class="ep-route-path">\${ep.route}</span>
      </div>
      <br>
      <a class="source-link" href="\${GITHUB}\${ep.path}" target="_blank">↗ View source on GitHub</a>
    </div>

    <div class="card">
      <div class="card-header">
        <span class="card-title">Request Parameters <span class="card-count">\${ep.params.length}</span></span>
      </div>
      \${paramsHtml}
    </div>

    <div class="card">
      <div class="card-header">
        <span class="card-title">SAP Response Tables <span class="card-count">\${ep.tables.length}</span></span>
      </div>
      <div class="chips">\${tablesHtml}</div>
    </div>

    <div class="card">
      <div class="card-header">
        <span class="card-title">Manage Data Lake Columns</span>
      </div>
      <div class="col-body">
        <div class="col-form">
          <div class="col-field">
            <label>Column Name</label>
            <input class="col-input" id="cname" placeholder="e.g. STORE_CODE">
          </div>
          <div class="col-field">
            <label>SQL Type</label>
            <input class="col-input" id="ctype" placeholder="NVARCHAR(255)" value="NVARCHAR(255)">
          </div>
        </div>
        <div class="col-btns">
          <button class="btn-add" onclick="addOp('ADD')">＋ Add Column</button>
          <button class="btn-rem" onclick="addOp('REMOVE')">－ Remove Column</button>
        </div>
        <div id="opsList" class="ops-list"></div>
        <button class="btn-apply" id="applyBtn" onclick="applyOps()" disabled>⚡ Apply to Data Lake</button>
        <div id="colRes" class="result-box"></div>
      </div>
    </div>
  \`;
}

function addOp(action) {
  const n = document.getElementById("cname").value.trim().toUpperCase();
  const t = document.getElementById("ctype").value.trim()||"NVARCHAR(255)";
  if (!n) { document.getElementById("cname").focus(); return; }
  colOps.push({action, column:n, sqlType:t});
  document.getElementById("cname").value = "";
  renderOps();
}

function remOp(i) { colOps.splice(i,1); renderOps(); }

function renderOps() {
  const el = document.getElementById("opsList");
  if (!el) return;
  el.innerHTML = colOps.map((op,i)=>\`
    <div class="op-row">
      <span class="\${op.action==="ADD"?"op-add":"op-rem"}">\${op.action==="ADD"?"＋":"－"}</span>
      <span class="op-col">\${op.column}</span>
      <span class="op-type">\${op.action==="ADD"?op.sqlType:""}</span>
      <button class="op-del" onclick="remOp(\${i})">✕</button>
    </div>\`).join("");
  const btn = document.getElementById("applyBtn");
  if (btn) btn.disabled = colOps.length===0;
}

async function applyOps() {
  if (!current||!colOps.length) return;
  const btn=document.getElementById("applyBtn"), res=document.getElementById("colRes");
  btn.disabled=true; btn.textContent="Applying...";
  const tableName = current.tables[0]||current.rfc.replace(/_RFC$/,"");
  try {
    const r = await fetch("/columns",{method:"POST",headers:{"Content-Type":"application/json"},
      body:JSON.stringify({tableName,operations:colOps})});
    const d = await r.json();
    if (r.ok) {
      res.className="result-box result-ok";
      res.textContent=\`✓ \${colOps.length} change(s) applied to \${tableName}. SQL migration pushed to GitHub.\`;
      colOps=[]; renderOps();
    } else throw new Error(d.error||"Unknown error");
  } catch(e) {
    res.className="result-box result-err";
    res.textContent="✗ "+e.message;
  }
  res.style.display="block";
  btn.textContent="⚡ Apply to Data Lake";
  btn.disabled=colOps.length===0;
}

init();
</script>
</body>
</html>`;
      return new Response(html, {headers:{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-cache'}});
    }




    // ── GET /cf-zones → list zones + create tunnel hostname ────────────────────
    if (url.pathname === '/cf-zones') {
      const CF_ACCOUNT = 'bab06c93e17ae71cae3c11b4cc40240b';
      const CF_KEY = env.CLOUDFLARE_API_KEY;
      const CF_EMAIL = 'Akash@v2kart.com';
      const h = {'X-Auth-Key':CF_KEY,'X-Auth-Email':CF_EMAIL,'Content-Type':'application/json'};
      const TUNNEL_ID = '7e73cc51-9b0b-4084-8f7b-44bc9c8f31a3';
      
      try {
        // List zones
        const zr = await fetch(`https://api.cloudflare.com/client/v4/zones?account.id=${CF_ACCOUNT}&per_page=50`, {headers:h});
        const zd = await zr.json();
        const zones = (zd.result||[]).map(z => ({id:z.id, name:z.name, status:z.status}));
        
        // For each zone, try to create CNAME for tunnel
        const results = [];
        for (const zone of zones) {
          // Try create CNAME: sap-api.zone.name → TUNNEL_ID.cfargotunnel.com
          const cname = await fetch(`https://api.cloudflare.com/client/v4/zones/${zone.id}/dns_records`, {
            method:'POST', headers:h,
            body: JSON.stringify({
              type:'CNAME', name:'sap-api', 
              content:`${TUNNEL_ID}.cfargotunnel.com`,
              proxied:true, ttl:1
            })
          });
          const cd = await cname.json();
          results.push({zone:zone.name, cnameSuccess:cd.success, errors:cd.errors||[], record:cd.result?.name});
        }
        
        return new Response(JSON.stringify({zones, cnameResults:results}),
          {headers:{'Content-Type':'application/json'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),{status:500,headers:{'Content-Type':'application/json'}});
      }
    }

    // ── GET /tunnel-test → test tunnel connectivity from worker ────────────────
    if (url.pathname === '/tunnel-test') {
      const TUNNEL_ID = '7e73cc51-9b0b-4084-8f7b-44bc9c8f31a3';
      const testUrl = `https://${TUNNEL_ID}.cfargotunnel.com/api/ZPO_DD_UPD_RFC/Post`;
      try {
        const r = await fetch(testUrl, {
          method: 'POST',
          headers: {'Content-Type':'application/json'},
          body: JSON.stringify({PO_NO:'4500001234',DELV_DATE:'20260401'}),
          signal: AbortSignal.timeout(15000)
        });
        const txt = await r.text();
        return new Response(JSON.stringify({
          tunnelUrl: testUrl, httpStatus: r.status, response: txt
        }), {headers:{'Content-Type':'application/json'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message, tunnelUrl:testUrl}),
          {status:500, headers:{'Content-Type':'application/json'}});
      }
    }

    // ── POST /tunnel-setup → create named CF tunnel (one-time setup) ──────────
    if (url.pathname === '/tunnel-setup' && request.method === 'POST') {
      const CF_ACCOUNT = 'bab06c93e17ae71cae3c11b4cc40240b';
      const CF_KEY     = env.CLOUDFLARE_API_KEY || env.CF_API_KEY || env.CLOUDFLARE_API_TOKEN;
      const CF_EMAIL   = 'Akash@v2kart.com';
      if (!CF_KEY) return new Response(JSON.stringify({error:'CLOUDFLARE_API_KEY secret not set on worker'}),
        {status:500, headers:{'Content-Type':'application/json'}});
      try {
        const TUNNEL_NAME = 'v2-sap-api';
        // Global API Key auth (X-Auth-Key + X-Auth-Email)
        const h = {'X-Auth-Key':CF_KEY,'X-Auth-Email':CF_EMAIL,'Content-Type':'application/json'};
        
        // Check existing
        let listR = await fetch(`https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}/cfd_tunnel?name=${TUNNEL_NAME}&is_deleted=false`, {headers:h});
        let listD = await listR.json();
        
        let tunnelId, tunnelToken;
        if (listD.result && listD.result.length > 0) {
          tunnelId = listD.result[0].id;
        } else {
          // Create it
          let cr = await fetch(`https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}/cfd_tunnel`,
            {method:'POST', headers:h, body:JSON.stringify({name:TUNNEL_NAME, config_src:'cloudflare'})});
          let cd = await cr.json();
          if (!cd.success) return new Response(JSON.stringify({error:'create failed', detail:cd}),
            {status:500, headers:{'Content-Type':'application/json'}});
          tunnelId = cd.result.id;
        }
        
        // Get token
        let tr = await fetch(`https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}/cfd_tunnel/${tunnelId}/token`, {headers:h});
        let td = await tr.json();
        if (!td.success) return new Response(JSON.stringify({error:'token failed', detail:td, listDetail:listD}),
          {status:500, headers:{'Content-Type':'application/json'}});
        tunnelToken = td.result;
        
        // Set ingress
        await fetch(`https://api.cloudflare.com/client/v4/accounts/${CF_ACCOUNT}/cfd_tunnel/${tunnelId}/configurations`,
          {method:'PUT', headers:h, body:JSON.stringify({config:{ingress:[{service:'http://localhost:9292'}]}})});
        
        return new Response(JSON.stringify({
          ok: true,
          tunnelId,
          tunnelToken,
          tunnelUrl: `https://${tunnelId}.cfargotunnel.com`,
          listApiResponse: listD
        }), {headers:{'Content-Type':'application/json'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),{status:500,headers:{'Content-Type':'application/json'}});
      }
    }

    // GET /sap-fetch → SAP Fetch UI
    if (url.pathname === '/data-lake' || url.pathname === '/data-lake/') {
      return new Response("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n  <meta charset=\"UTF-8\"/><meta name=\"viewport\" content=\"width=device-width,initial-scale=1.0\"/>\n  <title>V2 Retail \u2014 Data Lake API</title>\n  <link rel=\"stylesheet\" href=\"https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui.css\"/>\n  <style>\n    :root{--v2-blue:#1A3C6E;--v2-accent:#E8401C;--v2-light:#F4F7FB;}\n    body{margin:0;font-family:'Segoe UI',sans-serif;background:var(--v2-light);}\n    #topbar{background:var(--v2-blue);padding:14px 28px;display:flex;align-items:center;justify-content:space-between;box-shadow:0 2px 8px rgba(0,0,0,.25);}\n    .logo-text{color:#fff;font-size:20px;font-weight:700;}\n    .logo-sub{color:#a8c4e8;font-size:12px;}\n    .badge{background:var(--v2-accent);color:#fff;padding:4px 14px;border-radius:20px;font-size:12px;font-weight:600;}\n    #swagger-ui .topbar{display:none;}\n    .nav{padding:8px 28px;background:#f0f4fa;border-bottom:1px solid #dde3ee;display:flex;gap:10px;font-size:13px;align-items:center;}\n    .nav a{color:var(--v2-blue);text-decoration:none;padding:4px 12px;border-radius:4px;border:1px solid #c5d2e8;font-weight:500;}\n    .nav a:hover,.nav a.active{background:var(--v2-blue);color:#fff;border-color:var(--v2-blue);}\n    .info{background:#fff;padding:10px 28px;font-size:13px;color:#555;border-bottom:2px solid var(--v2-accent);display:flex;gap:24px;flex-wrap:wrap;}\n    .info b{color:var(--v2-blue);}\n  </style>\n</head>\n<body>\n<div id=\"topbar\">\n  <div><div class=\"logo-text\">V2 Retail \u00b7 Data Lake API</div><div class=\"logo-sub\">Azure DAB \u2192 DataV2 SQL Server @ 192.168.151.28</div></div>\n  <span class=\"badge\">82 TABLES \u00b7 LIVE</span>\n</div>\n<div class=\"nav\">\n  <a href=\"/explore\">RFC Explorer</a>\n  <a href=\"/sap-fetch\">SAP Fetch</a>\n  <a href=\"/data-lake\" class=\"active\">Data Lake</a>\n</div>\n<div class=\"info\">\n  <span>Source: <b>DataV2</b> @ 192.168.151.28</span>\n  <span>Via: <b>Azure DAB</b></span>\n  <span>Auth: <b>None</b></span>\n  <span>Protocol: <b>OData REST</b></span>\n  <span>Tables: <b>82</b></span>\n</div>\n<div id=\"swagger-ui\"></div>\n<script src=\"https://cdnjs.cloudflare.com/ajax/libs/swagger-ui/5.17.14/swagger-ui-bundle.js\"></script>\n<script>\nSwaggerUIBundle({\n  spec:{\"openapi\":\"3.0.1\",\"info\":{\"title\":\"V2 Retail Data Lake API\",\"version\":\"1.0.0\",\"description\":\"**82 tables** from DataV2 @ 192.168.151.28. OData: $filter $top $skip $select $orderby\"},\"servers\":[{\"url\":\"https://my-dab-app.azurewebsites.net\",\"description\":\"Azure DAB \\u2014 V2 Retail Data Lake\"}],\"paths\":{\"/api/API_Master_AKA\":{\"get\":{\"tags\":[\"API\"],\"summary\":\"API_Master_AKA\",\"operationId\":\"get_API_Master_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/BIN_MOV_ART_WISE_AKA\":{\"get\":{\"tags\":[\"BIN\"],\"summary\":\"BIN_MOV_ART_WISE_AKA\",\"operationId\":\"get_BIN_MOV_ART_WISE_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/BROADER_MENU\":{\"get\":{\"tags\":[\"BROADER\"],\"summary\":\"BROADER_MENU\",\"operationId\":\"get_BROADER_MENU\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/COMPANY_STOCK_GEN_ART_CLR_MASTER_AKA\":{\"get\":{\"tags\":[\"COMPANY\"],\"summary\":\"COMPANY_STOCK_GEN_ART_CLR_MASTER_AKA\",\"operationId\":\"get_COMPANY_STOCK_GEN_ART_CLR_MASTER_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/COMPANY_STOCK_MASTER_AKA\":{\"get\":{\"tags\":[\"COMPANY\"],\"summary\":\"COMPANY_STOCK_MASTER_AKA\",\"operationId\":\"get_COMPANY_STOCK_MASTER_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_CLR_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_CLR_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_CLR_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_FAB_ALL_BGT_AKA\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_FAB_ALL_BGT_AKA\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_FAB_ALL_BGT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_M_MVGR_ALL_BGT_AKA\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_M_MVGR_ALL_BGT_AKA\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_M_MVGR_ALL_BGT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_SZ_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_SZ_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_SZ_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_CO_MJ_VND_ALL_BGT_AKA\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_CO_MJ_VND_ALL_BGT_AKA\",\"operationId\":\"get_CO_BGT_AK_CO_MJ_VND_ALL_BGT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_ST_MJ_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_ST_MJ_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_ST_MJ_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_ST_MJ_CLR_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_ST_MJ_CLR_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_ST_MJ_CLR_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/CO_BGT_AK_ST_MJ_SZ_ALL_BGT\":{\"get\":{\"tags\":[\"CO\"],\"summary\":\"CO_BGT_AK_ST_MJ_SZ_ALL_BGT\",\"operationId\":\"get_CO_BGT_AK_ST_MJ_SZ_ALL_BGT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/C_ART_DATA\":{\"get\":{\"tags\":[\"C\"],\"summary\":\"C_ART_DATA\",\"operationId\":\"get_C_ART_DATA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/DAILY_SALE_DATA_GEN_ART_WISE\":{\"get\":{\"tags\":[\"DAILY\"],\"summary\":\"DAILY_SALE_DATA_GEN_ART_WISE\",\"operationId\":\"get_DAILY_SALE_DATA_GEN_ART_WISE\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/DAILY_SALE_DATA_GEN_CLR_WISE\":{\"get\":{\"tags\":[\"DAILY\"],\"summary\":\"DAILY_SALE_DATA_GEN_CLR_WISE\",\"operationId\":\"get_DAILY_SALE_DATA_GEN_CLR_WISE\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ ZADVANCE_PAYMENT\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ ZADVANCE_PAYMENT\",\"operationId\":\"get_ET_ ZADVANCE_PAYMENT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ADVANCE_PAYMENT\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ADVANCE_PAYMENT\",\"operationId\":\"get_ET_ADVANCE_PAYMENT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ARTICLE_GEN_CLR_WISE\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ARTICLE_GEN_CLR_WISE\",\"operationId\":\"get_ET_ARTICLE_GEN_CLR_WISE\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ART_Broader_Menu_DATA\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ART_Broader_Menu_DATA\",\"operationId\":\"get_ET_ART_Broader_Menu_DATA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_FINANCE_DOCUMENTS\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_FINANCE_DOCUMENTS\",\"operationId\":\"get_ET_FINANCE_DOCUMENTS\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_GENERATED\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_GENERATED\",\"operationId\":\"get_ET_GENERATED\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_PROJECT_OVERVIEW\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_PROJECT_OVERVIEW\",\"operationId\":\"get_ET_PROJECT_OVERVIEW\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_PUR_DATA_RPT_AKA\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_PUR_DATA_RPT_AKA\",\"operationId\":\"get_ET_PUR_DATA_RPT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_SALES_DATA\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_SALES_DATA\",\"operationId\":\"get_ET_SALES_DATA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_Supplier_Master\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_Supplier_Master\",\"operationId\":\"get_ET_Supplier_Master\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_VARIANT_ART_WISE\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_VARIANT_ART_WISE\",\"operationId\":\"get_ET_VARIANT_ART_WISE\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_VEND_DEDUCTION\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_VEND_DEDUCTION\",\"operationId\":\"get_ET_VEND_DEDUCTION\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_VEND_DEDUCTION_AKA\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_VEND_DEDUCTION_AKA\",\"operationId\":\"get_ET_VEND_DEDUCTION_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_VEND_PAY\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_VEND_PAY\",\"operationId\":\"get_ET_VEND_PAY\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZADVANCE_PAYMENT\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZADVANCE_PAYMENT\",\"operationId\":\"get_ET_ZADVANCE_PAYMENT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZART_BAR_DETAIL_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZART_BAR_DETAIL_RFC\",\"operationId\":\"get_ET_ZART_BAR_DETAIL_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZFBL1N_PAYMENT_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZFBL1N_PAYMENT_RFC\",\"operationId\":\"get_ET_ZFBL1N_PAYMENT_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZFI_FB65_DISCOUNT_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZFI_FB65_DISCOUNT_RFC\",\"operationId\":\"get_ET_ZFI_FB65_DISCOUNT_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZGET_STORE_MASTER\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZGET_STORE_MASTER\",\"operationId\":\"get_ET_ZGET_STORE_MASTER\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZMC_SIZE_MASTER_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZMC_SIZE_MASTER_RFC\",\"operationId\":\"get_ET_ZMC_SIZE_MASTER_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZMM_CITY_TRNS_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZMM_CITY_TRNS_RFC\",\"operationId\":\"get_ET_ZMM_CITY_TRNS_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZPO_MODIFICATION\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZPO_MODIFICATION\",\"operationId\":\"get_ET_ZPO_MODIFICATION\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZPO_MODIFICATION_RFC_AKA\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZPO_MODIFICATION_RFC_AKA\",\"operationId\":\"get_ET_ZPO_MODIFICATION_RFC_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZPO_MODIFY_REASON\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZPO_MODIFY_REASON\",\"operationId\":\"get_ET_ZPO_MODIFY_REASON\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZSRM_ROUTING_LOG_RFC\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZSRM_ROUTING_LOG_RFC\",\"operationId\":\"get_ET_ZSRM_ROUTING_LOG_RFC\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ET_ZTEST\":{\"get\":{\"tags\":[\"ET\"],\"summary\":\"ET_ZTEST\",\"operationId\":\"get_ET_ZTEST\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/GET_VV_ART_DATA_AKA\":{\"get\":{\"tags\":[\"GET\"],\"summary\":\"GET_VV_ART_DATA_AKA\",\"operationId\":\"get_GET_VV_ART_DATA_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/HHT_VERIENT_ART\":{\"get\":{\"tags\":[\"HHT\"],\"summary\":\"HHT_VERIENT_ART\",\"operationId\":\"get_HHT_VERIENT_ART\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_DC_MJ_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_DC_MJ_AKA\",\"operationId\":\"get_INVT_DC_MJ_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_DC_MJ_CAT_VND_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_DC_MJ_CAT_VND_AKA\",\"operationId\":\"get_INVT_DC_MJ_CAT_VND_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_CLR_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_CLR_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_CLR_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_FAB_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_FAB_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_FAB_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_MACRO_MVGR_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_MACRO_MVGR_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_MACRO_MVGR_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_MICRO_MVGR_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_MICRO_MVGR_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_MICRO_MVGR_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_MRP_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_MRP_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_MRP_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_RNG_SEG_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_RNG_SEG_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_RNG_SEG_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_SIZE_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_SIZE_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_SIZE_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/INVT_ST_MJ_CAT_VND_AKA\":{\"get\":{\"tags\":[\"INVT\"],\"summary\":\"INVT_ST_MJ_CAT_VND_AKA\",\"operationId\":\"get_INVT_ST_MJ_CAT_VND_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Issue_Tracker\":{\"get\":{\"tags\":[\"Issue\"],\"summary\":\"Issue_Tracker\",\"operationId\":\"get_Issue_Tracker\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Karma_Detailed_Report\":{\"get\":{\"tags\":[\"Karma\"],\"summary\":\"Karma_Detailed_Report\",\"operationId\":\"get_Karma_Detailed_Report\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Karma_Summary_Report\":{\"get\":{\"tags\":[\"Karma\"],\"summary\":\"Karma_Summary_Report\",\"operationId\":\"get_Karma_Summary_Report\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/L_ARTICLE_Sheet1\":{\"get\":{\"tags\":[\"L\"],\"summary\":\"L_ARTICLE_Sheet1\",\"operationId\":\"get_L_ARTICLE_Sheet1\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/MRDC_VAR_ART_AKA\":{\"get\":{\"tags\":[\"MRDC\"],\"summary\":\"MRDC_VAR_ART_AKA\",\"operationId\":\"get_MRDC_VAR_ART_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/MSA_STOCK_DATA_AKA\":{\"get\":{\"tags\":[\"MSA\"],\"summary\":\"MSA_STOCK_DATA_AKA\",\"operationId\":\"get_MSA_STOCK_DATA_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/PO_DATA_AKA\":{\"get\":{\"tags\":[\"PO\"],\"summary\":\"PO_DATA_AKA\",\"operationId\":\"get_PO_DATA_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Product_Master\":{\"get\":{\"tags\":[\"Product\"],\"summary\":\"Product_Master\",\"operationId\":\"get_Product_Master\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/RETAIL_BAR_CODE_AKA\":{\"get\":{\"tags\":[\"RETAIL\"],\"summary\":\"RETAIL_BAR_CODE_AKA\",\"operationId\":\"get_RETAIL_BAR_CODE_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ROI_TREND_DATA_AKA\":{\"get\":{\"tags\":[\"ROI\"],\"summary\":\"ROI_TREND_DATA_AKA\",\"operationId\":\"get_ROI_TREND_DATA_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/SALE_DATA_CM_AKA\":{\"get\":{\"tags\":[\"SALE\"],\"summary\":\"SALE_DATA_CM_AKA\",\"operationId\":\"get_SALE_DATA_CM_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/SALE_DATA_L_2_YEAR_MONTH_AKA\":{\"get\":{\"tags\":[\"SALE\"],\"summary\":\"SALE_DATA_L_2_YEAR_MONTH_AKA\",\"operationId\":\"get_SALE_DATA_L_2_YEAR_MONTH_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/STORE_MAJ_CAT_BUDGET_FIXTURE_AKA\":{\"get\":{\"tags\":[\"STORE\"],\"summary\":\"STORE_MAJ_CAT_BUDGET_FIXTURE_AKA\",\"operationId\":\"get_STORE_MAJ_CAT_BUDGET_FIXTURE_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/STORE_PLANT_MASTER_AKA\":{\"get\":{\"tags\":[\"STORE\"],\"summary\":\"STORE_PLANT_MASTER_AKA\",\"operationId\":\"get_STORE_PLANT_MASTER_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Sale_Data_L_2Years_MAJ_CAT_aka\":{\"get\":{\"tags\":[\"Sale\"],\"summary\":\"Sale_Data_L_2Years_MAJ_CAT_aka\",\"operationId\":\"get_Sale_Data_L_2Years_MAJ_CAT_aka\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Sale_Data_L_2Years_STORE_aka\":{\"get\":{\"tags\":[\"Sale\"],\"summary\":\"Sale_Data_L_2Years_STORE_aka\",\"operationId\":\"get_Sale_Data_L_2Years_STORE_aka\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/TBL_16_RETAIL_GND_BGT_FORMAT_Sheet1\":{\"get\":{\"tags\":[\"TBL\"],\"summary\":\"TBL_16_RETAIL_GND_BGT_FORMAT_Sheet1\",\"operationId\":\"get_TBL_16_RETAIL_GND_BGT_FORMAT_Sheet1\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/TNA_REPORT\":{\"get\":{\"tags\":[\"TNA\"],\"summary\":\"TNA_REPORT\",\"operationId\":\"get_TNA_REPORT\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/Task_Tracker\":{\"get\":{\"tags\":[\"Task\"],\"summary\":\"Task_Tracker\",\"operationId\":\"get_Task_Tracker\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/VIEW_SALE_GEN_CLR_ART_WISE\":{\"get\":{\"tags\":[\"VIEW\"],\"summary\":\"VIEW_SALE_GEN_CLR_ART_WISE\",\"operationId\":\"get_VIEW_SALE_GEN_CLR_ART_WISE\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/VW_ET_SALES_DATA_AKA\":{\"get\":{\"tags\":[\"VW\"],\"summary\":\"VW_ET_SALES_DATA_AKA\",\"operationId\":\"get_VW_ET_SALES_DATA_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/VW_GRC_REPORT_AKA\":{\"get\":{\"tags\":[\"VW\"],\"summary\":\"VW_GRC_REPORT_AKA\",\"operationId\":\"get_VW_GRC_REPORT_AKA\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/VW_GRC_REPORT_NEW\":{\"get\":{\"tags\":[\"VW\"],\"summary\":\"VW_GRC_REPORT_NEW\",\"operationId\":\"get_VW_GRC_REPORT_NEW\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/VW_LYSP_DAY_MAPPING\":{\"get\":{\"tags\":[\"VW\"],\"summary\":\"VW_LYSP_DAY_MAPPING\",\"operationId\":\"get_VW_LYSP_DAY_MAPPING\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/ZSRM_VEND_PAYMENT_INFO\":{\"get\":{\"tags\":[\"ZSRM\"],\"summary\":\"ZSRM_VEND_PAYMENT_INFO\",\"operationId\":\"get_ZSRM_VEND_PAYMENT_INFO\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}},\"/api/vw_PO_PENDING_New\":{\"get\":{\"tags\":[\"vw\"],\"summary\":\"vw_PO_PENDING_New\",\"operationId\":\"get_vw_PO_PENDING_New\",\"parameters\":[{\"name\":\"$filter\",\"in\":\"query\",\"schema\":{\"type\":\"string\"},\"description\":\"e.g. `STORE_CODE eq 'DL01'`\"},{\"name\":\"$top\",\"in\":\"query\",\"schema\":{\"type\":\"integer\",\"default\":100}},{\"name\":\"$skip\",\"in\":\"query\",\"schema\":{\"type\":\"integer\"}},{\"name\":\"$select\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}},{\"name\":\"$orderby\",\"in\":\"query\",\"schema\":{\"type\":\"string\"}}],\"responses\":{\"200\":{\"description\":\"OK\",\"content\":{\"application/json\":{\"schema\":{\"type\":\"object\"}}}}}}}}},\n  dom_id:'#swagger-ui',\n  deepLinking:true,\n  defaultModelsExpandDepth:-1,\n  defaultModelExpandDepth:1,\n  docExpansion:'none',\n  filter:true,\n  tryItOutEnabled:true,\n  persistAuthorization:true,\n  layout:'BaseLayout'\n});\n</script>\n</body>\n</html>", {headers:{'Content-Type':'text/html','Cache-Control':'public,max-age=300'}});
    }

        // ── POST /deploy → docx → Claude → C# controller → GitHub push ──────────
    if (url.pathname === '/deploy' && request.method === 'POST') {
      try {
        const body = await request.json();
        const { filename, content } = body;
        if (!content) return new Response(JSON.stringify({error:'content required'}),{status:400,headers:{'Content-Type':'application/json'}});

        // Generate job ID
        const jobId = crypto.randomUUID().replace(/-/g,'').substring(0,12);

        // Store initial status in KV
        await env.RFC_KV.put(`job:${jobId}`, JSON.stringify({
          status:'running', started: Date.now(),
          steps:{parse:'active',generate:'pending',push:'pending',build:'pending',dab:'pending',explorer:'pending'}
        }), {expirationTtl: 3600});

        // Process asynchronously using waitUntil
        ctx.waitUntil(runDeploy(jobId, filename, content, env));

        return new Response(JSON.stringify({jobId, status:'started'}),{
          headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}
        });
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),{status:500,headers:{'Content-Type':'application/json'}});
      }
    }


    // ── POST /relay-rfc → forward to IIS via CF tunnel → return SAP response ────
    if (url.pathname === '/relay-rfc' && request.method === 'POST') {
      try {
        const body = await request.json();
        const { rfc, params } = body;
        if (!rfc) return new Response(JSON.stringify({error:'rfc required'}),
          {status:400,headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
        const rfcRouteMap = {
          'ZPO_DD_UPD_RFC':'ZPO_DD_UPD_RFC/Post',
          'ZPO_MODIFICATION':'ZPO_MODIFICATION/Execute',
          'ZADVANCE_PAYMENT_RFC':'ZADVANCE_PAYMENT_RFC/Post',
          'ZSALES_MOP_RFC':'ZSALES_MOP_RFC/Post',
          'ZPO_MODIFICATION_RFC':'ZPO_MODIFICATION/Execute',
        };
        const route = rfcRouteMap[rfc] || (rfc + '/Post');
        const iisUrl = `${IIS_HOST}/api/${route}`;
        const iisResp = await fetch(iisUrl, {
          method:'POST',
          headers:{'Content-Type':'application/json'},
          body: JSON.stringify(params || {}),
          signal: AbortSignal.timeout(60000)
        });
        const raw = await iisResp.text();
        let data; try { data = JSON.parse(raw); } catch { data = {raw}; }
        const tableKey = data?.Data ? Object.keys(data.Data)[0] : null;
        const fetched = tableKey && Array.isArray(data.Data[tableKey]) ? data.Data[tableKey].length : 1;
        return new Response(JSON.stringify({
          ok:iisResp.ok, status:iisResp.status, rfc, fetched, stored:0, data
        }), {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
      } catch(e) {
        return new Response(JSON.stringify({error:e.message}),
          {status:500,headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
      }
    }


    // ── GET /status/{jobId} → poll deployment progress ────────────────────────
    if (url.pathname.startsWith('/status/')) {
      const jobId = url.pathname.split('/status/')[1];
      const data = await env.RFC_KV.get(`job:${jobId}`);
      if (!data) return new Response(JSON.stringify({error:'job not found'}),{status:404,headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
      return new Response(data, {headers:{'Content-Type':'application/json','Access-Control-Allow-Origin':'*'}});
    }

        if (url.pathname === '/sap-fetch' || url.pathname === '/sap-fetch/') {
      const html = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>V2 Retail · SAP Fetch</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
:root{--bg:#f4f6fb;--white:#fff;--border:#e2e6ef;--accent:#2563eb;--accent-bg:#eff4ff;
  --green:#16a34a;--green-bg:#f0fdf4;--red:#dc2626;--red-bg:#fef2f2;
  --text:#1a2035;--sub:#475569;--muted:#7a8499;--dim:#b0b8cc;
  --mono:Consolas,monospace;--sans:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif}
body{background:var(--bg);font-family:var(--sans);color:var(--text);min-height:100vh}
.top{background:#1a2035;height:52px;display:flex;align-items:center;justify-content:space-between;padding:0 24px;position:sticky;top:0;z-index:50}
.brand{display:flex;align-items:center;gap:8px;color:#fff;font-weight:700;font-size:15px}
.bdot{width:7px;height:7px;border-radius:50%;background:#34d399;animation:blink 2s infinite}
@keyframes blink{0%,100%{opacity:1}50%{opacity:.3}}
.badge{font-family:var(--mono);font-size:10px;background:rgba(255,255,255,.15);color:#a5f3fc;padding:2px 8px;border-radius:4px;font-weight:400}
.main{max-width:960px;margin:0 auto;padding:28px 20px}
h2{font-size:22px;font-weight:700;margin-bottom:4px}
.sub{color:var(--muted);font-size:13px;margin-bottom:24px}
.card{background:var(--white);border:1px solid var(--border);border-radius:10px;padding:20px;margin-bottom:16px;box-shadow:0 1px 3px rgba(0,0,0,.04)}
.card-title{font-size:10px;font-weight:700;letter-spacing:1.8px;text-transform:uppercase;color:var(--muted);margin-bottom:14px}
.grid2{display:grid;grid-template-columns:1fr 1fr;gap:12px}
.grid3{display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px}
label{font-size:11px;font-weight:600;letter-spacing:.8px;text-transform:uppercase;color:var(--sub);display:block;margin-bottom:5px}
input,select{width:100%;background:var(--bg);border:1.5px solid var(--border);border-radius:7px;padding:8px 11px;color:var(--text);font-size:13px;font-family:var(--sans);outline:none;transition:.15s}
input:focus,select:focus{border-color:var(--accent);background:var(--white);box-shadow:0 0 0 3px rgba(37,99,235,.07)}
input::placeholder{color:var(--dim)}
.cols-wrap{margin-top:12px}
.cols-grid{display:flex;flex-wrap:wrap;gap:8px;margin-top:8px}
.col-chip{display:flex;align-items:center;gap:5px;background:var(--accent-bg);border:1px solid #bfcfff;color:var(--accent);padding:4px 10px;border-radius:5px;font-size:12px;font-family:var(--mono);cursor:pointer;user-select:none}
.col-chip.off{background:#f8f9fc;border-color:var(--border);color:var(--muted)}
.col-chip input[type=checkbox]{display:none}
.btn{width:100%;background:var(--accent);color:#fff;border:none;border-radius:8px;padding:12px;font-size:14px;font-weight:600;cursor:pointer;transition:.15s;margin-top:4px}
.btn:hover{background:#1d4ed8}
.btn:disabled{opacity:.45;cursor:not-allowed}
.result{margin-top:16px;padding:14px;border-radius:8px;font-size:13px;display:none;word-break:break-all}
.result.ok{background:var(--green-bg);border:1px solid #bbf7d0;color:var(--green)}
.result.err{background:var(--red-bg);border:1px solid #fecaca;color:var(--red)}
.result.info{background:var(--accent-bg);border:1px solid #bfcfff;color:var(--accent)}
.stat-row{display:flex;gap:16px;flex-wrap:wrap;margin-top:10px}
.stat{background:var(--white);border:1px solid var(--border);border-radius:8px;padding:12px 18px;text-align:center;flex:1;min-width:100px}
.stat .n{font-family:var(--mono);font-size:24px;font-weight:700;color:var(--accent)}
.stat .l{font-size:11px;color:var(--muted);margin-top:2px}
.rows-table{width:100%;border-collapse:collapse;font-size:11.5px;font-family:var(--mono);margin-top:12px}
.rows-table th{background:var(--bg);padding:6px 10px;text-align:left;border-bottom:1px solid var(--border);color:var(--muted);font-size:10px;letter-spacing:1px;text-transform:uppercase}
.rows-table td{padding:6px 10px;border-bottom:1px solid var(--border);color:var(--sub)}
.rows-table tr:last-child td{border-bottom:none}
.rows-table tr:hover td{background:#fafbff}
.scroll-x{overflow-x:auto}
#rowCount{font-size:11px;color:var(--muted);margin-bottom:4px}
</style>
</head>
<body>
<div class="top">
  <div class="brand"><div class="bdot"></div>V2 Retail · SAP Fetch <span class="badge">V2DC-ADDVERB @ 192.168.151.36</span></div>
  <div style="color:#94a3b8;font-size:12px">Relay: v2-rfc-relay.azurewebsites.net</div>
</div>

<div class="main">
  <h2>SAP RFC → SQL Fetch</h2>
  <p class="sub">Select an RFC, set parameters, choose columns, then fetch data from SAP into <strong>claudetestv2</strong>.</p>

  <div class="card">
    <div class="card-title">1. Select RFC</div>
    <div class="grid2">
      <div>
        <label>RFC Function</label>
        <select id="rfcSelect" onchange="onRfcChange()">
          <optgroup label="── Finance ──">
            <option value="ZADVANCE_PAYMENT_RFC" data-table="ET_ZADVANCE_PAYMENT"
              data-cols="DOCUMENT_TYPE,COMPANY_CODE,DOCUMENT_NUMBER,FISCAL_YEAR,LINE_ITEM,POSTING_KEY,ACCOUNT_TYPE,SPECIAL_G_L_IND,TRANSACT_TYPE,DEBIT_CREDIT,AMOUNT_IN_LC,AMOUNT,TEXT,VENDOR,PAYMENT_AMT,POSTING_DATE"
              data-params="I_COMPANY_CODE,I_POSTING_DATE_LOW,I_POSTING_DATE_HIGH">
              ZADVANCE_PAYMENT_RFC — Advance Payment Documents
            </option>
          </optgroup>
          <optgroup label="── Gate Entry / LOT Putway ──">
            <option value="ZVND_PUTWAY_BIN_VAL_RFC" data-table="ET_ZVND_PUTWAY_BIN_VAL"
              data-cols="TYPE,MESSAGE" data-params="IM_USER,IM_PLANT,IM_BIN">
              ZVND_PUTWAY_BIN_VAL_RFC — BIN Validation
            </option>
            <option value="ZVND_PUTWAY_PALETTE_VAL_RFC" data-table="ET_ZVND_PUTWAY_PALETTE_VAL"
              data-cols="PO_NUMBER,VENDOR_CODE,VENDOR_NAME,INVOICE_NO,BOX_COUNT,PALETTE_NO"
              data-params="IM_USER,IM_PLANT,IM_BIN,IM_PALL">
              ZVND_PUTWAY_PALETTE_VAL_RFC — Palette Validation
            </option>
            <option value="ZVND_PUTWAY_SAVE_DATA_RFC" data-table="ET_ZVND_PUTWAY_SAVE"
              data-cols="TYPE,MESSAGE" data-params="IM_USER">
              ZVND_PUTWAY_SAVE_DATA_RFC — Save Putway Data [WRITE]
            </option>
            <option value="ZWM_GET_GATE_ENTRY_DATA_RFC" data-table="ET_ZWM_GET_GATE_ENTRY_DATA_RFC" data-params="">ZWM_GET_GATE_ENTRY_DATA_RFC</option>
            <option value="ZWM_GET_GATE_ENTRY_DATA4_RFC" data-table="ET_ZWM_GET_GATE_ENTRY_DATA4_RFC" data-params="">ZWM_GET_GATE_ENTRY_DATA4_RFC</option>
            <option value="ZWM_GET_GATE_ENTRY_LIST_RFC" data-table="ET_ZWM_GET_GATE_ENTRY_LIST_RFC" data-params="">ZWM_GET_GATE_ENTRY_LIST_RFC</option>
            <option value="ZWM_GET_GATE_ENTRY_LIST4_RFC" data-table="ET_ZWM_GET_GATE_ENTRY_LIST4_RFC" data-params="">ZWM_GET_GATE_ENTRY_LIST4_RFC</option>
            <option value="ZWM_GET_GATE_ENTRY_PALLATE_RFC" data-table="ET_ZWM_GET_GATE_ENTRY_PALLATE_RFC" data-params="">ZWM_GET_GATE_ENTRY_PALLATE_RFC</option>
            <option value="ZWM_GATE_BIN_VALIDATION3_N" data-table="ET_ZWM_GATE_BIN_VALIDATION3_N" data-params="">ZWM_GATE_BIN_VALIDATION3_N</option>
            <option value="ZWM_GATE_BIN_VALIDATION4_N" data-table="ET_ZWM_GATE_BIN_VALIDATION4_N" data-params="">ZWM_GATE_BIN_VALIDATION4_N</option>
            <option value="ZWM_GATE_BOX3N" data-table="ET_ZWM_GATE_BOX3N" data-params="">ZWM_GATE_BOX3N</option>
            <option value="ZWM_GATE_PALLATE_VALIDATE3_N" data-table="ET_ZWM_GATE_PALLATE_VALIDATE3_N" data-params="">ZWM_GATE_PALLATE_VALIDATE3_N</option>
            <option value="ZWM_GATE_PALLATE_VALIDATE4_N" data-table="ET_ZWM_GATE_PALLATE_VALIDATE4_N" data-params="">ZWM_GATE_PALLATE_VALIDATE4_N</option>
            <option value="ZWM_GATE_SAVE3_N" data-table="ET_ZWM_GATE_SAVE3_N" data-params="">ZWM_GATE_SAVE3_N</option>
          </optgroup>
          <optgroup label="── Inbound ──">
            <option value="ZVND_UNLOAD_HU_VALIDATE_RFC" data-table="ET_ZVND_UNLOAD_HU_VALIDATE_RFC" data-params="">ZVND_UNLOAD_HU_VALIDATE_RFC</option>
            <option value="ZVND_UNLOAD_PALLATE_VALIDATION" data-table="ET_ZVND_UNLOAD_PALLATE_VALIDATION" data-params="">ZVND_UNLOAD_PALLATE_VALIDATION</option>
            <option value="ZVND_UNLOAD_SAVE_RFC" data-table="ET_ZVND_UNLOAD_SAVE_RFC" data-params="">ZVND_UNLOAD_SAVE_RFC</option>
          </optgroup>
          <optgroup label="── DC Routing ──">
            <option value="ZDC_ROUTING_SUB_RFC" data-table="ET_ZDC_ROUTING_SUB_RFC" data-params="">ZDC_ROUTING_SUB_RFC</option>
            <option value="ZWM_GATE_ENTRY_RFC" data-table="ET_ZWM_GATE_ENTRY_RFC" data-params="">ZWM_GATE_ENTRY_RFC</option>
            <option value="ZWM_HU_STORE_TT_RFC" data-table="ET_ZWM_HU_STORE_TT_RFC" data-params="">ZWM_HU_STORE_TT_RFC</option>
            <option value="zwm_dc_routing_rfc" data-table="ET_zwm_dc_routing_rfc" data-params="">zwm_dc_routing_rfc</option>
          </optgroup>
          <optgroup label="── Vehicle Loading ──">
            <option value="ZWM_HUBWISE_STORE_LIST_RFC" data-table="ET_ZWM_HUBWISE_STORE_LIST_RFC" data-params="">ZWM_HUBWISE_STORE_LIST_RFC</option>
            <option value="ZWM_HU_SELECTION_RFC" data-table="ET_ZWM_HU_SELECTION_RFC" data-params="">ZWM_HU_SELECTION_RFC</option>
            <option value="ZWM_SAVE_SCANNEDHULIST_RFC" data-table="ET_ZWM_SAVE_SCANNEDHULIST_RFC" data-params="">ZWM_SAVE_SCANNEDHULIST_RFC</option>
            <option value="ZWM_TRANSPORTER_DETAILS_RFC" data-table="ET_ZWM_TRANSPORTER_DETAILS_RFC" data-params="">ZWM_TRANSPORTER_DETAILS_RFC</option>
          </optgroup>
          <optgroup label="── HU Scan ──">
            <option value="ZWM_SAVE_HU" data-table="ET_ZWM_SAVE_HU" data-params="">ZWM_SAVE_HU</option>
            <option value="ZWM_SCAN_HU" data-table="ET_ZWM_SCAN_HU" data-params="">ZWM_SCAN_HU</option>
          </optgroup>
          <optgroup label="── HU Creation ──">
            <option value="ZVND_HU_PUSH_API_POST" data-table="ET_ZVND_HU_PUSH_API_POST" data-params="">ZVND_HU_PUSH_API_POST</option>
            <option value="ZVND_HU_VALIDATE_RFC" data-table="ET_ZVND_HU_VALIDATE_RFC" data-params="">ZVND_HU_VALIDATE_RFC</option>
            <option value="ZWM_VEND_PO_HEADER" data-table="ET_ZWM_VEND_PO_HEADER" data-params="">ZWM_VEND_PO_HEADER</option>
          </optgroup>
          <optgroup label="── HRMS ──">
            <option value="ZESIC_MASTER_POST_RFC" data-table="ET_ZESIC_MASTER_POST_RFC" data-params="">ZESIC_MASTER_POST_RFC</option>
            <option value="ZHR_LEAVE_POLICY_RFC" data-table="ET_ZHR_LEAVE_POLICY_RFC" data-params="">ZHR_LEAVE_POLICY_RFC</option>
            <option value="ZLWF_MASTER_POST_RFC" data-table="ET_ZLWF_MASTER_POST_RFC" data-params="">ZLWF_MASTER_POST_RFC</option>
            <option value="ZPF_MASTER_POST_RFC" data-table="ET_ZPF_MASTER_POST_RFC" data-params="">ZPF_MASTER_POST_RFC</option>
            <option value="ZPT_MASTER_POST_RFC" data-table="ET_ZPT_MASTER_POST_RFC" data-params="">ZPT_MASTER_POST_RFC</option>
          </optgroup>
          <optgroup label="── Vendor SRM Routing ──">
            <option value="ZMM_ART_CREATION_RFC" data-table="ET_ZMM_ART_CREATION_RFC" data-params="">ZMM_ART_CREATION_RFC</option>
            <option value="ZPO_MODIFICATION" data-table="ET_ZPO_MODIFICATION" data-params="">ZPO_MODIFICATION</option>
          </optgroup>
          <optgroup label="── Finance (Other) ──">
            <option value="ZFINANCE_DOCUMENT_RFC" data-table="ET_ZFINANCE_DOCUMENT_RFC" data-params="">ZFINANCE_DOCUMENT_RFC</option>
            <option value="ZSALES_MOP_RFC" data-table="ET_ZSALES_MOP_RFC" data-params="">ZSALES_MOP_RFC</option>
          </optgroup>
        </select>
      </div>
      <div>
        <label>Target Table</label>
        <input id="targetTable" value="ET_ZADVANCE_PAYMENT" placeholder="SQL table name">
      </div>
    </div>
  </div>

  <div class="card">
    <div class="card-title">2. Parameters</div>
    <div id="paramsGrid" class="grid3"></div>
  </div>

  <div class="card">
    <div class="card-title">3. Options</div>
    <div class="grid2">
      <div>
        <label>Max Rows</label>
        <input id="maxRows" type="number" value="1000" min="1" max="50000">
      </div>
      <div>
        <label>Target Database</label>
        <input id="targetDb" value="claudetestv2">
      </div>
    </div>
    <div class="cols-wrap">
      <label>Columns to fetch (click to toggle)</label>
      <div id="colsGrid" class="cols-grid"></div>
    </div>
  </div>

  <button class="btn" id="fetchBtn" onclick="doFetch()">⚡ Fetch from SAP → Store in SQL</button>

  <div class="stat-row" id="statsRow" style="display:none">
    <div class="stat"><div class="n" id="sFetched">0</div><div class="l">Fetched</div></div>
    <div class="stat"><div class="n" id="sStored">0</div><div class="l">Stored</div></div>
    <div class="stat"><div class="n" id="sTotal">0</div><div class="l">SAP Total</div></div>
  </div>

  <div id="result" class="result"></div>

  <div id="previewWrap" style="display:none;margin-top:16px">
    <div id="rowCount"></div>
    <div class="scroll-x"><table class="rows-table" id="previewTable"></table></div>
  </div>
</div>

<script>
const RELAY = 'https://v2-rfc-relay.azurewebsites.net';

function onRfcChange() {
  const sel = document.getElementById('rfcSelect');
  const opt = sel.options[sel.selectedIndex];
  document.getElementById('targetTable').value = opt.dataset.table || '';

  // Build params inputs
  const params = (opt.dataset.params || '').split(',').filter(Boolean);
  const grid = document.getElementById('paramsGrid');
  grid.innerHTML = params.map(p => {
    const today = new Date().toISOString().slice(0,10).replace(/-/g,'');
    const val = p.includes('LOW') ? '20260101' : p.includes('HIGH') ? today : p.includes('CODE') ? '1000' : '';
    return \\\`<div><label>\\\${p}</label><input id="p_\\\${p}" value="\\\${val}" placeholder="\\\${p}"></div>\\\`;
  }).join('');

  // Build column chips
  const cols = (opt.dataset.cols || '').split(',').filter(Boolean);
  const cg = document.getElementById('colsGrid');
  cg.innerHTML = cols.map(c =>
    \\\`<label class="col-chip" id="chip_\\\${c}"><input type="checkbox" checked id="chk_\\\${c}" onchange="toggleChip('\\\${c}')">\\\${c}</label>\\\`
  ).join('');
}

function toggleChip(col) {
  const chk = document.getElementById('chk_'+col);
  const chip = document.getElementById('chip_'+col);
  chip.className = 'col-chip' + (chk.checked ? '' : ' off');
}

async function doFetch() {
  const sel = document.getElementById('rfcSelect');
  const opt = sel.options[sel.selectedIndex];
  const paramNames = (opt.dataset.params || '').split(',').filter(Boolean);
  const params = {};
  for (const p of paramNames) {
    params[p] = document.getElementById('p_'+p)?.value?.trim() || '';
  }
  const cols = Array.from(document.querySelectorAll('[id^=chk_]'))
    .filter(c => c.checked).map(c => c.id.replace('chk_',''));

  const btn = document.getElementById('fetchBtn');
  const res = document.getElementById('result');
  btn.disabled = true; btn.textContent = 'Fetching from SAP...';
  res.style.display = 'none';
  document.getElementById('statsRow').style.display = 'none';
  document.getElementById('previewWrap').style.display = 'none';

  try {
    const r = await fetch(RELAY+'/relay-rfc', {
      method: 'POST',
      headers: {'Content-Type':'application/json'},
      body: JSON.stringify({
        rfc: sel.value,
        params,
        columns: cols,
        maxRows: parseInt(document.getElementById('maxRows').value)||1000,
        targetTable: document.getElementById('targetTable').value,
        targetDb: document.getElementById('targetDb').value,
      })
    });
    const d = await r.json();

    if (!r.ok || d.error) {
      res.className='result err';
      res.textContent = '✗ ' + (d.error || JSON.stringify(d));
      res.style.display='block';
    } else {
      document.getElementById('sFetched').textContent = d.fetched || 0;
      document.getElementById('sStored').textContent = d.stored || 0;
      document.getElementById('sTotal').textContent = d.total_sap || 0;
      document.getElementById('statsRow').style.display = 'flex';

      res.className='result ok';
      res.textContent = '✓ Fetched '+d.fetched+' rows from SAP, stored '+d.stored+' in claudetestv2.'
        + (d.truncated ? ' (SAP had more — increase Max Rows to get all)' : '')
        + (d.message ? ' SAP: '+d.message : '');
      res.style.display='block';

      if (d.rows && d.rows.length > 0) {
        const keys = Object.keys(d.rows[0]);
        const head = '<thead><tr>'+keys.map(k=>'<th>'+k+'</th>').join('')+'</tr></thead>';
        const body = '<tbody>'+d.rows.slice(0,50).map(row =>
          '<tr>'+keys.map(k=>'<td>'+(row[k]??'')+'</td>').join('')+'</tr>'
        ).join('')+'</tbody>';
        document.getElementById('previewTable').innerHTML = head+body;
        document.getElementById('rowCount').textContent =
          'Showing first '+Math.min(50,d.rows.length)+' of '+d.rows.length+' rows';
        document.getElementById('previewWrap').style.display='block';
      }
    }
  } catch(e) {
    res.className='result err';
    res.textContent='✗ Network error: '+e.message;
    res.style.display='block';
  }

  btn.disabled=false; btn.textContent='⚡ Fetch from SAP → Store in SQL';
}

// Init on load
onRfcChange();
</script>
</body>
</html>`;
      return new Response(html, {headers:{'Content-Type':'text/html;charset=utf-8','Cache-Control':'no-cache'}});
    }

    return new Response('Not Found', {status:404});
  }
};



// CRON handled by IIS SyncController + Windows Task Scheduler (02:00 IST)