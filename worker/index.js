/**
 * V2 Retail · RFC Pipeline Worker
 * Upload RFC .docx → Parse → Generate → Push GitHub → Live API
 */

const GITHUB_REPO   = 'akash0631/rfc-api';
const GITHUB_BRANCH = 'master';
const DAB_APP_URL   = 'https://my-dab-app.azurewebsites.net';
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
  const entries = parseZip(bytes);
  const docXml = entries['word/document.xml'];
  if (!docXml) throw new Error('Not a valid .docx file (word/document.xml not found)');
  const xml = new TextDecoder().decode(docXml);
  // Strip XML tags, normalise whitespace
  return xml
    .replace(/<w:p[ >]/g, '\n<w:p ')
    .replace(/<\/w:p>/g, '\n')
    .replace(/<[^>]+>/g, ' ')
    .replace(/\s{2,}/g, ' ')
    .replace(/\n +/g, '\n')
    .trim();
}

async function parseZip(bytes) {
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
    // Only care about document.xml
    if (name === 'word/document.xml') {
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
async function parseRfc(text, apiKey) {
  const raw = await claude(`You are parsing a SAP RFC specification document for V2 Retail.
Extract the following and return ONLY valid JSON (no markdown, no explanation):
{
  "rfcName": "RFC function name e.g. ZADVANCE_PAYMENT_RFC",
  "description": "one-line description",
  "category": "one of: Finance,GateEntry,Vendor,HUCreation,FabricPutway,HRMS,NSO,PaperlessPicklist,Sampling,VehicleLoading",
  "importParams": [{"name":"PARAM","sapType":"TYPE","description":"what it is"}],
  "outputType": "table OR return_only",
  "outputTableName": "TABLE param name or null",
  "outputFields": [{"fieldName":"F","sapType":"T","length":"L"}],
  "suggestedSqlTable": "ET_RFCNAME (ET_ prefix, no _RFC suffix)"
}
RFC Document:
${text.slice(0,5000)}`, apiKey, 800);
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
SAP method: BaseController.${env.fn}
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
async function runPipeline(text, sapEnv, jobId, env) {
  const apiKey   = env.ANTHROPIC_API_KEY;
  const ghToken  = env.GITHUB_TOKEN;
  const kv       = env.RFC_JOBS;
  const log = async (step, status, detail='') => {
    const job = JSON.parse(await kv.get(jobId)||'{}');
    job.steps = job.steps||[];
    const existing = job.steps.find(s=>s.step===step);
    if (existing) { existing.status=status; existing.detail=detail; }
    else job.steps.push({step, status, detail});
    if (status==='done'||status==='error') {
      const allDone = job.steps.every(s=>s.status==='done'||s.status==='error');
      if (allDone) job.status = job.steps.some(s=>s.status==='error') ? 'error' : 'complete';
    }
    await kv.put(jobId, JSON.stringify(job), {expirationTtl:86400});
  };

  try {
    // Step 1: Parse
    await log('parse','running','Extracting RFC spec with Claude AI...');
    let spec;
    try { spec = await parseRfc(text, apiKey); }
    catch(e) { await log('parse','error',e.message); return; }
    await log('parse','done',`${spec.rfcName} · ${spec.category}`);

    // Step 2: Generate controller
    await log('controller','running','Generating ASP.NET C# controller...');
    let code;
    try { code = await genController(spec, sapEnv, apiKey); }
    catch(e) { await log('controller','error',e.message); return; }
    await log('controller','done',`${code.split('\n').length} lines generated`);

    // Step 3: Push controller
    await log('github','running','Pushing controller to GitHub...');
    let ctrlResult;
    try { ctrlResult = await pushController(spec, code, sapEnv, ghToken); }
    catch(e) { await log('github','error',e.message); return; }
    await log('github','done',`${ctrlResult.filePath} (${ctrlResult.commitSha})`);

    // Step 4: Register DAB
    await log('dab','running','Registering entity in DAB config...');
    let dabResult;
    try { dabResult = await registerDab(spec, ghToken); }
    catch(e) { await log('dab','error',e.message); return; }
    await log('dab','done',`${dabResult.endpoint}`);

    // Step 5: Update Swagger
    await log('swagger','running','Updating Swagger documentation...');
    try { await updateSwagger(spec, sapEnv, ghToken); }
    catch(e) { await log('swagger','error',e.message); }
    await log('swagger','done','Endpoint card added to portal');

    // Final: write summary
    const job = JSON.parse(await kv.get(jobId)||'{}');
    job.status = 'complete';
    job.rfcName = spec.rfcName;
    job.rfcApi  = `POST /api/${spec.rfcName}`;
    job.dataLake = dabResult.endpoint;
    job.swagger  = 'https://v2-rfc-portal.pages.dev/swagger';
    job.commit   = ctrlResult.commitUrl;
    await kv.put(jobId, JSON.stringify(job), {expirationTtl:86400});

  } catch(e) {
    const job = JSON.parse(await kv.get(jobId)||'{}');
    job.status='error'; job.error=e.message;
    await kv.put(jobId, JSON.stringify(job), {expirationTtl:86400});
  }
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
    <a href="/swagger" style="margin-left:8px">Swagger →</a>
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
      <div class="drop" id="drop" onclick="document.getElementById('fileInput').click()"
           ondragover="event.preventDefault();this.classList.add('over')"
           ondragleave="this.classList.remove('over')"
           ondrop="handleDrop(event)">
        <div class="drop-icon">📄</div>
        <div class="drop-title">Drop your RFC document here</div>
        <div class="drop-sub">Supports .docx · .txt · .md</div>
        <input type="file" id="fileInput" accept=".docx,.txt,.md" onchange="handleFile(this.files[0])" style="display:none">
      </div>
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

    <button class="btn" id="deployBtn" disabled onclick="deploy()">
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
  const id = 'cr'+colRowCount;
  const opts = SQL_TYPES.map(t=>'<option>'+t+'</option>').join('');
  const row = document.createElement('div');
  row.className='col-row'; row.id=id;
  row.innerHTML = '<select onchange="toggleType(''+id+'',this.value)"><option'+(defaultAction==='ADD'?' selected':'')+'>ADD</option><option'+(defaultAction==='REMOVE'?' selected':'')+'>REMOVE</option></select>'
    + '<input placeholder="COLUMN_NAME" oninput="this.value=this.value.toUpperCase()" />'
    + '<select>'+opts+'</select>'
    + '<button class="rm" onclick="document.getElementById(''+id+'').remove()">×</button>';
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

function handleFile(file) {
  if (!file) return;
  selectedFile = file;
  document.getElementById('fileSel').style.display='flex';
  document.getElementById('fileName').textContent = file.name;
  document.getElementById('drop').classList.remove('over');
  document.getElementById('deployBtn').disabled = false;
}
function handleDrop(e) {
  e.preventDefault();
  document.getElementById('drop').classList.remove('over');
  handleFile(e.dataTransfer.files[0]);
}
function selEnv(env) {
  selectedEnv = env;
  ['dev','quality','production'].forEach(e => {
    document.getElementById('env-'+e).classList.toggle('sel', e===env);
  });
}

async function deploy() {
  if (!selectedFile) return;
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

function pollStatus(jobId) {
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
    '<span>RFC API (live via IIS after CI deploy)</span>' + (job.rfcApi||'') +
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
  ['parse','controller','github','dab','swagger'].forEach(s=>{
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
<meta http-equiv="refresh" content="0;url=https://v2-rfc-portal.pages.dev/swagger">
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
      try {
        const ab = await file.arrayBuffer();
        const fname = file.name.toLowerCase();
        if (fname.endsWith('.docx')) {
          text = await extractDocxText(ab);
        } else {
          text = new TextDecoder().decode(ab);
        }
      } catch(e) {
        return new Response(JSON.stringify({error:'Failed to read file: '+e.message}),
          {status:400, headers:{'Content-Type':'application/json'}});
      }

      // Run pipeline in background (non-blocking)
      // Use ctx.waitUntil so the Worker stays alive for the full pipeline
      ctx.waitUntil(runPipeline(text, sapEnv, jobId, env));

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

    return new Response('Not Found', {status:404});
  }
};
