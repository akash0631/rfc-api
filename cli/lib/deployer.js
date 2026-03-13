const fs = require('fs');
const path = require('path');
const axios = require('axios');
const { getFile, putFile } = require('./github');
const cfg = require('./config');

// ─────────────────────────────────────────────
// 1. Push controller to GitHub
// ─────────────────────────────────────────────
async function pushController(rfcSpec, controllerCode, sapEnv) {
  const folder = cfg.folderMap[rfcSpec.category] || 'Controllers/NSO';
  const filePath = `${folder}/${rfcSpec.rfcName}Controller.cs`;
  const { sha } = await getFile(filePath);
  const result = await putFile(
    filePath,
    controllerCode,
    sha,
    `Add ${rfcSpec.rfcName} controller [${sapEnv.toUpperCase()}]\n\nAuto-generated via v2rfc CLI`
  );
  return { filePath, ...result };
}

// ─────────────────────────────────────────────
// 2. Register entity in dab-config.json
// ─────────────────────────────────────────────
async function registerDabEntity(rfcSpec) {
  const sqlTable = rfcSpec.suggestedSqlTable || `ET_${rfcSpec.rfcName.replace(/_RFC$/, '')}`;
  const { content, sha } = await getFile(cfg.github.files.dabConfig);
  if (!content) throw new Error('dab-config.json not found in repo');

  const config = JSON.parse(content);

  if (!config.entities) config.entities = {};

  config.entities[sqlTable] = {
    source: {
      object: `dbo.${sqlTable}`,
      type: 'table',
      'key-fields': ['ID']
    },
    permissions: [
      { role: 'anonymous', actions: [{ action: 'read' }] }
    ],
    rest: {
      enabled: true,
      path: `/api/${sqlTable}`
    },
    graphql: { enabled: false }
  };

  const result = await putFile(
    cfg.github.files.dabConfig,
    JSON.stringify(config, null, 2),
    sha,
    `Register ${sqlTable} in DAB config for ${rfcSpec.rfcName}`
  );

  return {
    sqlTable,
    endpoint: `${cfg.azure.dabAppUrl}/api/${sqlTable}`,
    ...result
  };
}

// ─────────────────────────────────────────────
// 3. Inject endpoint card into Swagger HTML doc
// ─────────────────────────────────────────────
async function updateSwaggerDoc(rfcSpec, sapEnv) {
  const { content, sha } = await getFile(cfg.github.files.swagger);
  const env = cfg.sap.environments[sapEnv];
  const envColors = { dev: '#16a34a', quality: '#d97706', production: '#dc2626' };
  const envBg    = { dev: '#f0fdf4', quality: '#fffbeb', production: '#fef2f2' };
  const color = envColors[sapEnv] || '#4361ee';
  const bg    = envBg[sapEnv]    || '#eef1fd';

  const paramsJson = JSON.stringify(
    Object.fromEntries((rfcSpec.importParams || []).map(p => [p.name, `[${p.sapType}]`])),
    null, 2
  );

  const card = `
    <!-- AUTO:${rfcSpec.rfcName}:${new Date().toISOString().slice(0,10)} -->
    <div style="border:1px solid #e4e8f0;border-radius:10px;padding:16px;margin-bottom:12px;background:#fff;">
      <div style="display:flex;align-items:center;gap:10px;margin-bottom:10px;flex-wrap:wrap;">
        <span style="background:#4361ee;color:#fff;border-radius:5px;padding:3px 10px;font-size:11px;font-weight:700;font-family:monospace;">POST</span>
        <code style="font-size:13px;font-weight:600;">/api/${rfcSpec.rfcName}</code>
        <span style="background:${bg};border:1px solid ${color};color:${color};padding:2px 9px;border-radius:5px;font-size:11px;font-family:monospace;">${sapEnv.toUpperCase()} · ${env.host}</span>
      </div>
      <p style="font-size:12px;color:#475569;margin-bottom:10px;">${rfcSpec.description}</p>
      <pre style="background:#13141f;color:#9aa5d4;padding:12px;border-radius:7px;font-size:11px;overflow-x:auto;">${paramsJson}</pre>
      ${rfcSpec.outputType === 'table' ? `
      <div style="margin-top:8px;font-size:11px;color:#64748b;font-family:monospace;">
        ↓ Returns: <strong>${rfcSpec.outputTableName}</strong> array
        · Data Lake: <code>${cfg.azure.dabAppUrl}/api/${rfcSpec.suggestedSqlTable}</code>
      </div>` : ''}
    </div>`;

  let html = '';
  if (content) {
    html = content.includes('<!-- END ENDPOINTS -->')
      ? content.replace('<!-- END ENDPOINTS -->', card + '\n    <!-- END ENDPOINTS -->')
      : content.replace('</body>', card + '\n</body>');
  } else {
    html = `<!DOCTYPE html><html><head><meta charset="utf-8"><title>V2 RFC API</title></head><body style="font-family:sans-serif;max-width:900px;margin:40px auto;padding:0 20px;">
<h1 style="font-size:20px;margin-bottom:8px;">V2 Retail · RFC API Endpoints</h1>
<p style="font-size:12px;color:#64748b;margin-bottom:20px;font-family:monospace;">IIS: ${cfg.azure.iisServer} · Data Lake: ${cfg.azure.dabAppUrl}</p>
\n${card}\n    <!-- END ENDPOINTS -->\n</body></html>`;
  }

  const result = await putFile(cfg.github.files.swagger, html, sha, `Swagger: add ${rfcSpec.rfcName} endpoint`);
  return result;
}

// ─────────────────────────────────────────────
// 4. Save pipeline project files to GitHub
// ─────────────────────────────────────────────
async function pushPipelineProject(rfcSpec, pipelineCode, sapEnv) {
  const sqlTable = rfcSpec.suggestedSqlTable || `ET_${rfcSpec.rfcName.replace(/_RFC$/, '')}`;
  const basePath = `DataPipelines/${rfcSpec.rfcName}`;
  const results = [];

  // Program.cs
  if (pipelineCode.programCs) {
    const { sha } = await getFile(`${basePath}/Program.cs`);
    const r = await putFile(`${basePath}/Program.cs`, pipelineCode.programCs, sha,
      `Pipeline: ${rfcSpec.rfcName} → dbo.${sqlTable}`);
    results.push({ file: 'Program.cs', ...r });
  }

  // App.config
  if (pipelineCode.appConfig) {
    const { sha } = await getFile(`${basePath}/App.config`);
    const r = await putFile(`${basePath}/App.config`, pipelineCode.appConfig, sha,
      `Pipeline config: ${rfcSpec.rfcName}`);
    results.push({ file: 'App.config', ...r });
  }

  // README.md
  const readme = `# ${rfcSpec.rfcName} — Data Pipeline

## What this does
Pulls \`${rfcSpec.outputTableName || 'ET_DATA'}\` from SAP RFC \`${rfcSpec.rfcName}\` and loads into \`dbo.${sqlTable}\` on SQL Server.

## SAP Environment
- Host: ${cfg.sap.environments[sapEnv].host} / Client ${cfg.sap.environments[sapEnv].client}
- RFC: ${rfcSpec.rfcName}

## Data Lake REST API (via Azure DAB)
\`\`\`
GET ${cfg.azure.dabAppUrl}/api/${sqlTable}
GET ${cfg.azure.dabAppUrl}/api/${sqlTable}?$filter=COMPANY_CODE eq '1000'
GET ${cfg.azure.dabAppUrl}/api/${sqlTable}?$orderby=POSTING_DATE desc&$top=100
\`\`\`

## Schedule
Run \`Program.exe\` daily via Windows Task Scheduler.
No args = yesterday's date. With args: \`Program.exe YYYYMMDD YYYYMMDD COMPANY_CODE\`

## Swagger
Portal: ${cfg.cloudflare.swaggerUrl}
`;

  const { sha: rSha } = await getFile(`${basePath}/README.md`);
  const rr = await putFile(`${basePath}/README.md`, readme, rSha, `Pipeline README: ${rfcSpec.rfcName}`);
  results.push({ file: 'README.md', ...rr });

  return { basePath, sqlTable, results };
}

// ─────────────────────────────────────────────
// Main orchestrator — runs all 4 steps
// ─────────────────────────────────────────────
async function deployAll(rfcSpec, controllerCode, pipelineCode, sapEnv, options = {}) {
  const { skipSwagger = false, skipDab = false, skipPipeline = false } = options;
  const results = {};

  // Step 1: Controller → GitHub
  results.controller = await pushController(rfcSpec, controllerCode, sapEnv);

  // Step 2: DAB registration
  if (!skipDab) {
    results.dab = await registerDabEntity(rfcSpec);
  }

  // Step 3: Swagger doc
  if (!skipSwagger) {
    results.swagger = await updateSwaggerDoc(rfcSpec, sapEnv);
  }

  // Step 4: Pipeline project
  if (!skipPipeline && pipelineCode) {
    results.pipeline = await pushPipelineProject(rfcSpec, pipelineCode, sapEnv);
  }

  return results;
}

module.exports = { deployAll, pushController, registerDabEntity, updateSwaggerDoc, pushPipelineProject };
