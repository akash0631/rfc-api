const fs = require('fs');
const path = require('path');
const mammoth = require('mammoth');
const axios = require('axios');
const cfg = require('./config');

/**
 * Parse an RFC .docx file and extract structured metadata using Claude
 */
async function parseRfcDoc(docxPath) {
  if (!fs.existsSync(docxPath)) throw new Error(`File not found: ${docxPath}`);

  const ext = path.extname(docxPath).toLowerCase();
  let text = '';

  if (ext === '.docx') {
    const result = await mammoth.extractRawText({ path: docxPath });
    text = result.value;
  } else if (ext === '.txt' || ext === '.md') {
    text = fs.readFileSync(docxPath, 'utf8');
  } else {
    throw new Error(`Unsupported file type: ${ext}. Use .docx or .txt`);
  }

  if (!text.trim()) throw new Error('Document appears empty');

  // Use Claude to parse the RFC spec
  const prompt = `You are parsing a SAP RFC specification document for V2 Retail.
Extract the following information from this RFC document and return ONLY valid JSON (no markdown, no explanation):

{
  "rfcName": "RFC function name (e.g. ZADVANCE_PAYMENT_RFC)",
  "description": "one-line description of what this RFC does",
  "category": "one of: Finance, GateEntry, Vendor, HUCreation, FabricPutway, HRMS, NSO, PaperlessPicklist, Sampling, VehicleLoading",
  "importParams": [
    { "name": "PARAM_NAME", "sapType": "SAP_TYPE", "description": "what it means" }
  ],
  "outputType": "table OR return_only",
  "outputTableName": "name of the TABLE/EXPORT parameter if outputType is table, else null",
  "outputFields": [
    { "fieldName": "FIELD", "sapType": "TYPE", "length": "LENGTH" }
  ],
  "suggestedSqlTable": "ET_RFCNAME (without _RFC suffix, prefixed ET_)"
}

Rules:
- importParams are the IMPORT parameters the caller passes IN
- outputTableName is the TABLE or EXPORT parameter that returns data rows
- If there is no table output (only EX_RETURN/status), set outputType to "return_only"
- suggestedSqlTable should be the snake_case SQL table name for the data lake

RFC Document:
${text.slice(0, 6000)}`;

  const res = await axios.post('https://api.anthropic.com/v1/messages', {
    model: cfg.anthropic.model,
    max_tokens: 1000,
    messages: [{ role: 'user', content: prompt }]
  }, {
    headers: { 'Content-Type': 'application/json', 'x-api-key': process.env.ANTHROPIC_API_KEY || '' }
  });

  const raw = res.data.content?.find(b => b.type === 'text')?.text || '';
  const cleaned = raw.replace(/```json\n?/g, '').replace(/```/g, '').trim();

  try {
    return JSON.parse(cleaned);
  } catch (e) {
    throw new Error(`Failed to parse Claude response as JSON: ${cleaned.slice(0, 200)}`);
  }
}

module.exports = { parseRfcDoc };
