var __defProp = Object.defineProperty;
var __name = (target, value) => __defProp(target, "name", { value, configurable: true });

// worker_updated.js
var DATAV2_URL = "https://sql28.v2retail.net";
var DATAV2_KEY = "v2-datav2-analyst-2026";
var CLAUDE_URL = "https://api.anthropic.com/v1/messages";
var CORS_H = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "Content-Type,x-api-key",
  "Access-Control-Allow-Methods": "POST,GET,OPTIONS"
};
function J(body, status = 200) {
  return new Response(
    JSON.stringify(body, null, 2),
    { status, headers: { ...CORS_H, "Content-Type": "application/json" } }
  );
}
__name(J, "J");
var SYSTEM = 'You are the V2 Retail AI Data Analyst. Convert business questions into T-SQL for SQL Server 2019 (DataV2 database).\n\nCRITICAL RULES:\n- ALWAYS use WITH(NOLOCK)\n- STORE_STOCK_SALE_DAY_SUMMARY (1.2B rows): ALWAYS filter TXN_DATE max 90 days AND ST_CD\n- ET_STOCK_DATA_LOG (4.4B rows): ALWAYS filter WERKS + TOP clause\n- BUDAT in ET_GRC_DATA is varchar YYYYMMDD: use CAST(BUDAT AS DATE)\n- MATNR=18-digit, ARTICLE_NUMBER=10-digit; join: LEFT(MATNR,10)=ARTICLE_NUMBER\n- Use DAILY_SALE_PROCESS_DATA for fast rolling windows (L7D/L30D/L3M/LW/LM pre-computed)\n\nPRODUCT HIERARCHY (join: DY_ET_ART_GEN_DATA on ARTICLE_NUMBER):\nSegment(WWGHA_01:APP/GM) > Division(DIVISON:MENS/LADIES/KIDS/FW) > Sub-Division(SUB-DIVISON) > Major-Cat(MAJ-CAT) > MC-Code > Gen-Article(10-digit) > MATNR(18-digit)\n\nSTORE HIERARCHY (join: tbl_SAP_yogesh_ajay_Store_Master on ST_CD=STORE_CODE=WERKS):\nZone(NORTH/EAST/WEST/SOUTH) > Region(R1-R6) > Area > Store(H-prefix ~320) > Hub(DH-prefix ~15) > DC\n\nTIME: LW=last week, LM=last month, L7D=last 7 days, L30D=last 30 days, L3M=last 3 months, MTD=month to date, YTD=year to date\n\nSAP COLUMNS: MATNR=Article(18-digit), WERKS=Plant/Store, LGORT=Storage Loc(0001=Main), BWART=Mvt Type(101=Inward,261=Issue), BUDAT=Posting Date(varchar YYYYMMDD), MBLNR=Material Doc, MEINS=UOM, DMBTR=Amount INR\n\nKEY TABLES:\n- DAILY_SALE_PROCESS_DATA: pre-aggregated rolling windows per MATNR x STORE_CODE. Best for dashboards.\n- STORE_STOCK_SALE_DAY_SUMMARY: day-by-day detail 1.2B rows - filter TXN_DATE+ST_CD always\n- DY_ET_ART_GEN_DATA: article master with full hierarchy (primary dimension table)\n- tbl_SAP_yogesh_ajay_Store_Master: store master (ZONE,REG,AREA,ST_CD,STATE)\n- ET_GRC_DATA: GRC inward (MBLNR,BUDAT varchar,BWART=101 inward/102 return,MATNR 18-digit)\n- PO_DATA_AKA: purchase orders\n- ET_GOODS_MVT: all goods movements (transfers,adjustments)\n- TREND_KPI_DATA: pivoted KPIs (PARTICULARS_NAME=metric,PARTICULARS_VALUE=value)\n- bgt_act_daywise: budget vs actual by day+store+category\n- ET_STK0001_L30D_VALUES: daily stock snapshots last 30 days\n- STOREAREA_MASTER: store-article-date level stock+area data\n- ALLOCATION_REPORT_DATA: allocation details by store+article\n- KPI_TRANSACTION_DATA: transaction-level KPI data\n- ACTUAL_TD_STK_SALE_DATA: today stock and sale actuals\n\nRespond ONLY with JSON: {sql, explanation, chart_type, chart_config:{x_axis,y_axis}, follow_up:[3 questions]}';
function buildUI() {
  const suggestions = [
    "Yesterday sales by store \u2014 top 20 by value",
    "L3M sales for Mens Upper by Major Category",
    "Top 10 vendors by GRC value this month",
    "Store-wise sellthrough for Kids division L30D",
    "Pending POs older than 30 days by vendor",
    "Week by week sales trend last 12 weeks North zone",
    "Articles with SDR under 15 days at store level",
    "LYSM vs this month sales Ladies division by major category"
  ];
  const sugHtml = suggestions.map((s) => '<div class="sug" onclick="doAsk(this)">' + s + "</div>").join("");
  return '<!DOCTYPE html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>V2 AI Analyst</title><link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet"><style>*{box-sizing:border-box;margin:0;padding:0}body{background:#f5f7fa;color:#111827;font-family:Inter,sans-serif;min-height:100vh;display:flex;flex-direction:column}.hdr{background:#fff;border-bottom:1px solid #e5e7eb;padding:14px 24px;display:flex;align-items:center;gap:12px;position:sticky;top:0;z-index:50}.logo{background:#1B4F72;color:#fff;font-family:"JetBrains Mono",monospace;font-weight:700;font-size:11px;padding:6px 10px;border-radius:7px}.badge{display:inline-flex;align-items:center;gap:4px;font-family:"JetBrains Mono",monospace;font-size:9px;font-weight:700;padding:3px 8px;border-radius:20px;background:#d1fae5;border:1px solid #6ee7b7;color:#065f46}.dot{width:5px;height:5px;border-radius:50%;background:currentColor;animation:pu 1.4s infinite}@keyframes pu{0%,100%{opacity:1}50%{opacity:.2}}.main{flex:1;max-width:1200px;width:100%;margin:0 auto;padding:20px 24px;display:flex;flex-direction:column;gap:14px}.sugs{display:flex;flex-wrap:wrap;gap:8px}.sug{background:#fff;border:1px solid #e5e7eb;border-radius:8px;padding:8px 14px;font-size:12px;cursor:pointer;transition:all .15s;color:#374151}.sug:hover{border-color:#1B4F72;color:#1B4F72;background:#f0f7ff}.chat{display:flex;flex-direction:column;gap:12px}.mu{display:flex;justify-content:flex-end}.ma{display:flex;justify-content:flex-start}.bu{max-width:75%;padding:12px 16px;background:#1B4F72;color:#fff;border-radius:12px 12px 4px 12px;font-size:14px;line-height:1.6}.ba{max-width:92%;padding:14px 16px;background:#fff;border:1px solid #e5e7eb;border-radius:12px 12px 12px 4px;font-size:13px}.sql-b{background:#1e1e2e;color:#a6e3a1;font-family:"JetBrains Mono",monospace;font-size:11px;padding:14px;border-radius:8px;overflow-x:auto;margin:8px 0;white-space:pre-wrap;word-break:break-all}.rw{overflow-x:auto;border-radius:8px;border:1px solid #e5e7eb;margin:8px 0;max-height:380px}.rw table{width:100%;border-collapse:collapse;font-size:11px}.rw th{background:#1B4F72;color:#fff;padding:7px 12px;text-align:left;font-weight:600;white-space:nowrap;position:sticky;top:0}.rw td{padding:6px 12px;border-bottom:1px solid #f3f4f6;white-space:nowrap}.rw tr:nth-child(even) td{background:#f9fafb}.lbl{font-size:10px;font-weight:600;color:#9ca3af;letter-spacing:.08em;margin-bottom:4px;text-transform:uppercase}.expl{font-size:12px;color:#374151;background:#f0f7ff;padding:10px 14px;border-radius:8px;border-left:3px solid #1B4F72;margin-bottom:8px;line-height:1.6}.fups{display:flex;flex-wrap:wrap;gap:6px;margin-top:8px}.fup{font-size:11px;background:#f0f7ff;border:1px solid #bfdbfe;color:#1e40af;padding:4px 10px;border-radius:6px;cursor:pointer}.fup:hover{background:#dbeafe}.cw{margin:8px 0;border-radius:8px;border:1px solid #e5e7eb;background:#fff;padding:12px}.err{background:#fef2f2;border:1px solid #fecaca;color:#991b1b;padding:10px 14px;border-radius:8px;font-size:13px}.ia{display:flex;gap:10px;background:#fff;border:2px solid #e5e7eb;border-radius:12px;padding:10px 14px;position:sticky;bottom:16px;transition:border-color .15s;box-shadow:0 2px 8px rgba(0,0,0,.08)}.ia:focus-within{border-color:#1B4F72}.ia textarea{flex:1;border:none;outline:none;resize:none;font-family:Inter,sans-serif;font-size:14px;background:transparent;line-height:1.5;min-height:48px;max-height:120px}.sb{background:#1B4F72;color:#fff;border:none;padding:10px 20px;border-radius:8px;cursor:pointer;font-weight:600;font-size:13px;align-self:flex-end}.sb:hover{opacity:.85}.sb:disabled{opacity:.4;cursor:not-allowed}.ld{display:flex;gap:4px;align-items:center;padding:8px 0}.ld span{width:6px;height:6px;border-radius:50%;background:#9ca3af;animation:b .8s infinite}.ld span:nth-child(2){animation-delay:.15s}.ld span:nth-child(3){animation-delay:.3s}@keyframes b{0%,80%,100%{transform:scale(1)}40%{transform:scale(1.4)}}</style></head><body><div class="hdr"><div class="logo">V2 DATA</div><div style="flex:1"><div style="font-weight:700;font-size:15px">AI Data Analyst</div><div style="font-size:11px;color:#6b7280">DataV2 &middot; 1,332 tables &middot; 8B+ rows &middot; Ask anything in plain English</div></div><span class="badge"><span class="dot"></span>LIVE</span></div><div class="main"><div class="sugs" id="sugs">' + sugHtml + `</div><div class="chat" id="chat"></div><div class="ia"><textarea id="q" placeholder="Ask anything... e.g. Show me LW vs LYSW sales for Mens Bottom by store in North zone"></textarea><button class="sb" id="sb" onclick="sendQ()">Ask</button></div></div><script src="https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js"><\/script><script>
var chat=document.getElementById("chat"),qEl=document.getElementById("q"),btn=document.getElementById("sb");
var charts={},chatHistory=[];
document.getElementById("q").addEventListener("keydown",function(e){if(e.key==="Enter"&&!e.shiftKey){e.preventDefault();sendQ();}});
function doAsk(el){qEl.value=el.textContent;document.getElementById("sugs").style.display="none";sendQ();}
function addMsg(html,role){var d=document.createElement("div");d.className=role==="user"?"mu":"ma";d.innerHTML=html;chat.appendChild(d);d.scrollIntoView({behavior:"smooth",block:"end"});return d;}
function fmt(v){if(v===null||v===undefined)return"-";if(typeof v==="number"){if(Math.abs(v)>=10000000)return(v/10000000).toFixed(1)+"Cr";if(Math.abs(v)>=100000)return(v/100000).toFixed(1)+"L";if(Math.abs(v)>=1000)return(v/1000).toFixed(1)+"K";return v.toLocaleString("en-IN",{maximumFractionDigits:2});}return String(v);}
function mkTbl(data,cols){if(!data||!data.length)return "<p style='color:#9ca3af;font-size:11px'>No rows</p>";var h="<div class='rw'><table><thead><tr>";cols.forEach(function(c){h+="<th>"+c+"</th>";});h+="</tr></thead><tbody>";data.slice(0,1000).forEach(function(row){h+="<tr>";cols.forEach(function(c){var v=row[c],n=typeof v==="number";h+="<td style='"+(n?"text-align:right;font-family:JetBrains Mono,monospace":"")+"'>"+(v===null||v===undefined?"-":fmt(v))+"</td>";});h+="</tr>";});return h+"</tbody></table></div>";}
function mkChart(data,cols,type,cfg,id){if(!type||type==="none"||type==="table"||!data.length)return "";var xC=cfg&&cfg.x_axis?cfg.x_axis:cols[0],yC=cfg&&cfg.y_axis?cfg.y_axis:cols[1];setTimeout(function(){var ctx=document.getElementById(id);if(!ctx)return;if(charts[id]){charts[id].destroy();}var lbls=data.slice(0,30).map(function(r){return String(r[xC]||"").slice(0,25);});var vals=data.slice(0,30).map(function(r){return parseFloat(r[yC])||0;});var clrs=vals.map(function(_,i){return "hsla("+(195+i*10)+",65%,45%,.8)";});charts[id]=new Chart(ctx.getContext("2d"),{type:type==="line"?"line":type==="pie"?"doughnut":"bar",data:{labels:lbls,datasets:[{label:yC,data:vals,backgroundColor:clrs,borderColor:clrs.map(function(c){return c.replace(".8","1");}),borderWidth:1,tension:.3}]},options:{responsive:true,plugins:{legend:{display:type==="pie"}},scales:type!=="pie"?{y:{beginAtZero:true,ticks:{callback:function(v){return fmt(v);}}}}:{}}});},80);return "<div class='cw'><canvas id='"+id+"' height='80'></canvas></div>";}
async async function sendQ(){var q=qEl.value.trim();if(!q)return;btn.disabled=true;qEl.value="";document.getElementById("sugs").style.display="none";addMsg("<div class='bu'>"+q+"</div>","user");var ld=addMsg("<div class='ba'><div class='ld'><span></span><span></span><span></span></div></div>","ai");chatHistory.push({role:"user",content:q});try{var res=await fetch("/ask",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({question:q,history:chatHistory.slice(-6)})});var d=await res.json();ld.remove();var cid="c"+Date.now();var html="<div class='ba'>";if(d.explanation)html+="<div class='expl'>"+d.explanation+"</div>";if(d.sql)html+="<div class='lbl'>SQL</div><div class='sql-b'>"+d.sql+"</div>";if(d.result&&d.result.rows&&d.result.rows.length){html+="<div class='lbl'>RESULT &mdash; "+d.result.count+" rows</div>";html+=mkTbl(d.result.rows,Object.keys((d.result.rows&&d.result.rows[0])||{}));html+=mkChart(d.result.rows,Object.keys((d.result.rows&&d.result.rows[0])||{}),d.chart_type,d.chart_config,cid);}if(d.error&&!d.result)html+="<div class='err'>"+d.error+"</div>";if(d.follow_up&&d.follow_up.length){html+="<div class='fups'>";d.follow_up.forEach(function(f){html+="<span class='fup' onclick='qEl.value=this.textContent;sendQ();'>"+f+"</span>";});html+="</div>";}html+="</div>";addMsg(html,"ai");chatHistory.push({role:"assistant",content:d.explanation||"Done"});}catch(e){ld.remove();addMsg("<div class='ba'><div class='err'>Error: "+e.message+"</div></div>","ai");}btn.disabled=false;}
<\/script></body></html>`;
}
__name(buildUI, "buildUI");
var worker_updated_default = {
  async fetch(request, env) {
    const url = new URL(request.url);
    if (request.method === "OPTIONS") return new Response("", { headers: CORS_H });
    if (request.method === "GET" && (url.pathname === "/" || url.pathname === ""))
      return new Response(buildUI(), { headers: { ...CORS_H, "Content-Type": "text/html;charset=UTF-8" } });
    if (url.pathname === "/health") {
      try {
        const h = await fetch(DATAV2_URL + "/health", { headers: { "x-api-key": DATAV2_KEY }, signal: AbortSignal.timeout(5e3) });
        return J({ status: "ok", datav2: await h.json(), ts: (/* @__PURE__ */ new Date()).toISOString() });
      } catch (e) {
        return J({ status: "error", err: e.message });
      }
    }
    if (url.pathname === "/ask" && request.method === "POST") {
      let body;
      try {
        body = await request.json();
      } catch {
        return J({ error: "Invalid JSON" }, 400);
      }
      const q = (body.question || "").trim();
      if (!q) return J({ error: "question required" }, 400);
      const CLAUDE_KEY = env.CLAUDE_API_KEY;
      if (!CLAUDE_KEY) return J({ error: "CLAUDE_API_KEY not configured \u2014 add as Worker secret" }, 500);
      let meta;
      try {
        const msgs = [...(body.history || []).slice(-4).map((m) => ({ role: m.role, content: String(m.content) })), { role: "user", content: q }];
        const cr = await fetch(CLAUDE_URL, {
          method: "POST",
          headers: { "Content-Type": "application/json", "x-api-key": CLAUDE_KEY, "anthropic-version": "2023-06-01" },
          body: JSON.stringify({ model: "claude-sonnet-4-6", max_tokens: 2e3, system: SYSTEM, messages: msgs })
        });
        const cd = await cr.json();
        const raw = cd.content && cd.content[0] && cd.content[0].text || "";
        meta = JSON.parse(raw.replace(/^```json\n?/, "").replace(/\n?```$/, "").trim());
      } catch (e) {
        return J({ error: "AI error: " + e.message }, 500);
      }
      if (!meta.sql) return J({ error: "No SQL generated", raw: meta }, 500);
      let result = null, sqlErr = null;
      try {
        const qr = await fetch(DATAV2_URL + "/query", {
          method: "POST",
          headers: { "Content-Type": "application/json", "x-api-key": DATAV2_KEY },
          body: JSON.stringify({ sql: meta.sql }),
          signal: AbortSignal.timeout(9e4)
        });
        result = await qr.json();
        if (!result.success) sqlErr = result.error;
      } catch (e) {
        sqlErr = "DataV2 error: " + e.message;
      }
      return J({
        sql: meta.sql,
        explanation: meta.explanation,
        chart_type: meta.chart_type || "table",
        chart_config: meta.chart_config || {},
        follow_up: meta.follow_up || [],
        result,
        error: sqlErr
      });
    }
    if (url.pathname === "/query" && request.method === "POST") {
      if (request.headers.get("x-api-key") !== DATAV2_KEY) return J({ error: "Unauthorized" }, 401);
      const b = await request.json();
      const qr = await fetch(DATAV2_URL + "/query", { method: "POST", headers: { "Content-Type": "application/json", "x-api-key": DATAV2_KEY }, body: JSON.stringify(b) });
      return new Response(await qr.text(), { headers: { ...CORS_H, "Content-Type": "application/json" } });
    }
    return J({ error: "Not found" }, 404);
  }
};
export {
  worker_updated_default as default
};
//# sourceMappingURL=worker_updated.js.map
