const DATAV2_URL = "https://sql28.v2retail.net";
const DATAV2_BASE = "/api/datav2";
const DATAV2_KEY = "v2-datav2-analyst-2026";
const CLAUDE_URL = "https://api.anthropic.com/v1/messages";
const CORS_H = {
  "Access-Control-Allow-Origin":"*",
  "Access-Control-Allow-Headers":"Content-Type,x-api-key,x-claude-key",
  "Access-Control-Allow-Methods":"POST,GET,OPTIONS"
};

function J(body,status=200){
  return new Response(JSON.stringify(body,null,2),
    {status,headers:{...CORS_H,"Content-Type":"application/json"}});
}

const SYSTEM = `You are the V2 Retail AI Data Analyst. Convert business questions into T-SQL for SQL Server 2019 (DataV2 database on 192.168.151.28). 
Respond ONLY with valid JSON matching this schema exactly:
{"sql":"...","explanation":"...","chart_type":"bar|line|pie|table|none","chart_config":{"x_axis":"col","y_axis":"col"},"follow_up":["q1","q2","q3"]}

CRITICAL QUERY RULES:
- ALWAYS use WITH(NOLOCK) on every table
- STORE_STOCK_SALE_DAY_SUMMARY (1.2B rows): ALWAYS filter TXN_DATE (max 90 days) AND ST_CD — never without both
- ET_STOCK_DATA_LOG (4.4B rows): ALWAYS filter WERKS + TOP clause
- BUDAT in ET_GRC_DATA is varchar YYYYMMDD: use CAST(BUDAT AS DATE) for filtering
- MATNR=18-digit; ARTICLE_NUMBER=10-digit; join: LEFT(MATNR,10)=ARTICLE_NUMBER
- Use DAILY_SALE_PROCESS_DATA for fast rolling window queries — columns pre-computed
- Never SELECT * on large tables

PRODUCT HIERARCHY (join: DY_ET_ART_GEN_DATA on ARTICLE_NUMBER):
Segment(WWGHA_01: APP/GM/FAB) > Division(DIVISON: MENS/LADIES/KIDS/FW/GM_W) > Sub-Division(SUB-DIVISON) > Major-Cat(MAJ-CAT) > MC-Code > Gen-Article(10-digit) > MATNR(18-digit)

STORE HIERARCHY (join: tbl_SAP_yogesh_ajay_Store_Master on ST_CD=STORE_CODE=WERKS):
Zone(NORTH/EAST/WEST/SOUTH) > Region(R1-R6) > Area > Store(H-prefix ~320 stores) > Hub(DH-prefix ~15) > DC

TIME SHORTCUTS: LW=last week, LM=last month, L7D=last 7 days, L30D=last 30 days, L3M=last 3 months, MTD=month to date, YTD=year to date, LYSM=last year same month

SAP COLUMN TRANSLATIONS:
MATNR=Article(18-digit), WERKS=Plant/Store, LGORT=Storage Location(0001=Main,RM01=Return), BWART=Movement Type(101=GR Inward,102=GR Return,261=Goods Issue), BUDAT=Posting Date(varchar YYYYMMDD), MBLNR=Material Document No, MEINS=Unit of Measure, DMBTR=Amount in INR, LABST=Unrestricted Stock, TRAME=In-Transit Stock

DAILY_SALE_PROCESS_DATA columns (use brackets for special chars):
[L-3 MONTH  SALES Q]=L3M Sales Qty, [L-3 MONTH  SALES V]=L3M Sales Value,
[L-7 DAYS SALE-Q]=L7D Sales Qty, [L-7 DAYS SALE-V]=L7D Sales Value,
[LAST 30-DAYS_Sale_Q]=L30D Sales Qty, [LAST 30-DAYS_Sale_V]=L30D Sales Value,
LM_SALE_Q=Last Month Qty, LM_SALE_V=Last Month Value,
LW_Sale_Q=Last Week Qty, LW_Sale_V=Last Week Value,
LYSM_SALE_Q=Last Year Same Month Qty, LYSM_SALE_V=Last Year Same Month Value,
MTD_SALE_Q=Month To Date Qty, YTD_SALE_Q=Year To Date Qty,
TD_SALE_Q=Today Sales Qty, TD_SALE_V=Today Sales Value

KEY TABLES (top by usage):
- DAILY_SALE_PROCESS_DATA: pre-aggregated rolling windows per MATNR x STORE_CODE. Best for dashboards.
- STORE_STOCK_SALE_DAY_SUMMARY: day-by-day 1.2B rows — MUST filter TXN_DATE+ST_CD
- DY_ET_ART_GEN_DATA: article master with full hierarchy. Primary dimension table.
- tbl_SAP_yogesh_ajay_Store_Master: store master (ZONE,REG,AREA,ST_CD,STATE,ST_AREA,OLD_NEW,ST_OP_DT)
- ET_GRC_DATA: GRC inward (MBLNR,BUDAT varchar YYYYMMDD,BWART 101=inward,MATNR 18-digit,WERKS)
- PO_DATA_AKA: purchase orders (PO_NO,PO_TYPE NB=Standard,PO_CR_DATE,MATNR,WERKS)
- ET_GOODS_MVT: all goods movements (MBLNR,BWART,MATNR,WERKS,BLDAT,BUDAT)
- TREND_KPI_DATA: pivoted KPIs per MATNR x STORE_CODE x Dates (PARTICULARS_NAME=metric, PARTICULARS_VALUE=value)
- bgt_act_daywise: budget vs actual by date x store_cd x maj_cat (sloc,seg,div,sub_div)
- ET_STK0001_L30D_VALUES: daily stock snapshots last 30 days (MATNR,WERKS,stock_Date,STK_V,STK_Q)
- STOREAREA_MASTER: stock+area per store x article x date (809M rows — filter ST_CD+DATES)
- KPI_TRANSACTION_DATA: transaction KPIs (WWGHB_05,PARTICULARS_NAME,PARTICULARS_VALUE)
- ACTUAL_TD_STK_SALE_DATA: today actuals (STORE_CODE,MATNR,DATES,SALE_V,SALE_Q)
- ET_STOCK_SEASONAL_DATA: seasonal stock by MATNR x WERKS x LGORT (LABST=unrestricted qty)
- ALLOCATION_REPORT_DATA: allocation by STORE_CODE x SEG x DIV x MAJ_CAT
- MRST_PROCESSDATA_AGING_BKP: stock aging 0-90,91-180,181-360,360+ days by MATNR x STORE_CODE
- Festival_Rolling_Plan_Transaction_Data: festival plan actuals (Store_code,Maj_Cat,PARTICULARS_NAME,PARTICULARS_VALUE)

PERFORMANCE RULES — MUST FOLLOW:
1. For ANY sales query (LW/LM/MTD/YTD/L7D/L30D/LYSM/today), ALWAYS use DAILY_SALE_PROCESS_DATA — NEVER join to STORE_STOCK_SALE_DAY_SUMMARY for rolling metrics
2. DAILY_SALE_PROCESS_DATA already has all rolling windows pre-computed. Just GROUP BY STORE_CODE and join to tbl_SAP_yogesh_ajay_Store_Master for zone/region
3. For zone breakdown: GROUP BY zone from store master join — ONE simple join only
4. NEVER join more than 2 tables in a single query
5. NEVER use STORE_STOCK_SALE_DAY_SUMMARY unless specifically asked for a specific date range with BOTH ST_CD and TXN_DATE filters
6. TOP 50 max on any query
7. For budget vs actual: use bgt_act_daywise (sloc, seg, div, sub_div, store_cd, maj_cat columns)

FAST QUERY PATTERNS (always prefer these):
- Sales by zone: SELECT s.ZONE, SUM(d.LW_Sale_V) FROM DAILY_SALE_PROCESS_DATA d JOIN tbl_SAP_yogesh_ajay_Store_Master s ON d.STORE_CODE=s.ST_CD GROUP BY s.ZONE
- Sales totals: SELECT SUM(MTD_SALE_V), SUM(YTD_SALE_V) FROM DAILY_SALE_PROCESS_DATA WITH(NOLOCK)
- Top stores: SELECT STORE_CODE, SUM(MTD_SALE_V) FROM DAILY_SALE_PROCESS_DATA WITH(NOLOCK) GROUP BY STORE_CODE ORDER BY SUM(MTD_SALE_V) DESC`;

export default {
  async fetch(request, env) {
    const url = new URL(request.url);

    if (request.method === "OPTIONS") {
      return new Response(null,{status:204,headers:CORS_H});
    }

    if (url.pathname === "/health") {
      try {
        const h = await fetch(DATAV2_URL+DATAV2_BASE+"/health",{headers:{"User-Agent":"cf-worker"},signal:AbortSignal.timeout(5000)});
        return J({status:"ok",datav2:await h.json(),ts:new Date().toISOString()});
      } catch(e){ return J({status:"error",err:e.message},503); }
    }

    // Direct SQL query passthrough
    if (url.pathname === "/query" && request.method === "POST") {
      try {
        const b = await request.json();
        const qr = await fetch(DATAV2_URL+DATAV2_BASE+"/query",{
          method:"POST",
          headers:{"Content-Type":"application/json","User-Agent":"cf-worker"},
          body:JSON.stringify(b)
        });
        return new Response(await qr.text(),{headers:{...CORS_H,"Content-Type":"application/json"}});
      } catch(e){ return J({error:e.message},500); }
    }

    // AI analyst endpoint
    if (url.pathname === "/ask" && request.method === "POST") {
      let question="", history=[];
      try {
        const b = await request.json();
        question = b.question || "";
        history = b.history || [];
      } catch(e){ return J({error:"Invalid JSON body"},400); }

      if (!question) return J({error:"No question provided"},400);

      const apiKey = env.CLAUDE_API_KEY || request.headers.get("x-claude-key") || "";
      if (!apiKey) return J({error:"CLAUDE_API_KEY not configured"},500);

      let sql="", explanation="", chart_type="none", chart_config={}, follow_up=[], result=null, sqlErr=null;

      try {
        // Build messages
        const messages = [
          ...history.slice(-6).map(h=>({role:h.role,content:h.content})),
          {role:"user",content:question}
        ];

        const aiResp = await fetch(CLAUDE_URL, {
          method:"POST",
          headers:{
            "Content-Type":"application/json",
            "x-api-key":apiKey,
            "anthropic-version":"2023-06-01"
          },
          body:JSON.stringify({
            model:"claude-sonnet-4-20250514",
            max_tokens:1500,
            system:SYSTEM,
            messages
          }),
          signal:AbortSignal.timeout(25000)
        });

        const aiData = await aiResp.json();
        const rawText = aiData.content?.[0]?.text || "{}";

        // Parse JSON from AI response
        const jsonMatch = rawText.match(/\{[\s\S]*\}/);
        if (jsonMatch) {
          const parsed = JSON.parse(jsonMatch[0]);
          sql = parsed.sql || "";
          explanation = parsed.explanation || "";
          chart_type = parsed.chart_type || "none";
          chart_config = parsed.chart_config || {};
          follow_up = parsed.follow_up || [];
        }
      } catch(e) { sqlErr = "AI error: "+e.message; }

      // Execute SQL
      if (sql && !sqlErr) {
        try {
          const qr = await fetch(DATAV2_URL+DATAV2_BASE+"/query", {
            method:"POST",
            headers:{"Content-Type":"application/json","User-Agent":"cf-worker"},
            body:JSON.stringify({sql}),
            signal:AbortSignal.timeout(28000)
          });
          result = await qr.json();
        } catch(e) { sqlErr = "SQL error: "+e.message; }
      }

      return J({sql,explanation,chart_type,chart_config,follow_up,result,error:sqlErr});
    }

    // Serve the UI
    const HTML = `<!DOCTYPE html><html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>V2 AI Data Analyst</title><script src="https://cdnjs.cloudflare.com/ajax/libs/Chart.js/4.4.0/chart.umd.min.js"></script><style>*{box-sizing:border-box;margin:0;padding:0}body{font-family:'Segoe UI',Arial,sans-serif;background:#f0f2f7;height:100vh;display:flex;flex-direction:column}.topbar{background:#fff;border-bottom:1.5px solid #e0e4ef;padding:0 20px;height:52px;display:flex;align-items:center;justify-content:space-between;flex-shrink:0}.logo{display:flex;align-items:center;gap:10px}.logo-mark{width:28px;height:28px;background:#1F4E79;border-radius:6px;display:grid;place-items:center;font-weight:800;color:#fff;font-size:11px}.logo-name{font-size:14px;font-weight:700;color:#1F4E79}.logo-sub{font-size:11px;color:#888;margin-left:4px}.badge{background:#e8f4fd;color:#1F4E79;padding:3px 8px;border-radius:12px;font-size:11px;font-weight:600}.chat-wrap{flex:1;overflow-y:auto;padding:20px;display:flex;flex-direction:column;gap:12px}.mu{align-self:flex-end;max-width:70%}.ma{align-self:flex-start;max-width:90%;width:100%}.bu{background:#1F4E79;color:#fff;padding:10px 16px;border-radius:16px 16px 4px 16px;font-size:14px}.ba{background:#fff;border:1px solid #e0e4ef;padding:14px 16px;border-radius:4px 16px 16px 16px;font-size:14px}.expl{color:#374151;margin-bottom:8px;line-height:1.5}.sql-b{background:#1e1e2e;color:#cdd6f4;padding:10px 14px;border-radius:8px;font-family:monospace;font-size:12px;margin:8px 0;overflow-x:auto;white-space:pre-wrap}.lbl{font-size:11px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:.5px;margin:8px 0 4px}.rw{overflow-x:auto;margin:6px 0}table{border-collapse:collapse;width:100%;font-size:12px}th{background:#1F4E79;color:#fff;padding:6px 10px;text-align:left;font-weight:600}td{border:1px solid #e5e7eb;padding:5px 10px;color:#374151}tr:nth-child(even){background:#f9fafb}.chart-c{margin:10px 0;max-height:280px}.err{color:#dc2626;background:#fef2f2;padding:8px 12px;border-radius:8px;font-size:13px}.fups{display:flex;flex-wrap:wrap;gap:6px;margin-top:10px}.fup{background:#eff6ff;border:1px solid #bfdbfe;color:#1d4ed8;padding:5px 10px;border-radius:20px;font-size:12px;cursor:pointer}.fup:hover{background:#dbeafe}.ld{display:flex;gap:6px;align-items:center;padding:8px 0}.ld span{width:8px;height:8px;background:#93c5fd;border-radius:50%;animation:pulse 1.2s infinite}.ld span:nth-child(2){animation-delay:.2s}.ld span:nth-child(3){animation-delay:.4s}@keyframes pulse{0%,100%{opacity:.3;transform:scale(.8)}50%{opacity:1;transform:scale(1.2)}}.input-bar{background:#fff;border-top:1.5px solid #e0e4ef;padding:14px 20px;display:flex;gap:10px;flex-shrink:0}textarea{flex:1;border:1.5px solid #e0e4ef;border-radius:10px;padding:10px 14px;font-size:14px;resize:none;font-family:inherit;max-height:120px;overflow-y:auto}textarea:focus{outline:none;border-color:#1F4E79}#sb{background:#1F4E79;color:#fff;border:none;border-radius:10px;padding:10px 20px;font-size:14px;font-weight:600;cursor:pointer;white-space:nowrap}#sb:hover{background:#2E75B6}#sb:disabled{background:#93c5fd}.sugs{padding:0 20px 10px;display:flex;flex-wrap:wrap;gap:6px}.sugs span{background:#fff;border:1px solid #e0e4ef;color:#374151;padding:6px 12px;border-radius:20px;font-size:12px;cursor:pointer;font-weight:500}.sugs span:hover{border-color:#1F4E79;color:#1F4E79}</style></head><body><div class="topbar"><div class="logo"><div class="logo-mark">V2</div><div><div class="logo-name">AI Data Analyst<span class="logo-sub">DataV2 · 1,332 tables · 15B+ rows</span></div></div></div><span class="badge">&#x25CF; LIVE</span></div><div class="chat-wrap" id="chat"></div><div class="sugs" id="sugs"><span onclick="doAsk(this)">LW vs LYSW sales by zone</span><span onclick="doAsk(this)">Top 10 stores by L30D sales</span><span onclick="doAsk(this)">Stock coverage days by division</span><span onclick="doAsk(this)">GRC inward this month by DC</span><span onclick="doAsk(this)">Budget achievement MTD by region</span></div><div class="input-bar"><textarea id="q" rows="1" placeholder="Ask anything... e.g. Show me LW vs LYSW sales for Mens Bottom by store in North zone"></textarea><button id="sb" onclick="sendQ()">Ask</button></div><script>var chat=document.getElementById("chat"),qEl=document.getElementById("q"),btn=document.getElementById("sb");var charts={},chatHistory=[];document.getElementById("q").addEventListener("keydown",function(e){if(e.key==="Enter"&&!e.shiftKey){e.preventDefault();sendQ();}});function doAsk(el){qEl.value=el.textContent;document.getElementById("sugs").style.display="none";sendQ();}function addMsg(html,role){var d=document.createElement("div");d.className=role==="user"?"mu":"ma";d.innerHTML=html;chat.appendChild(d);d.scrollIntoView({behavior:"smooth",block:"end"});return d;}function fmt(v){if(v===null||v===undefined)return"-";if(typeof v==="number"){if(Math.abs(v)>=10000000)return(v/10000000).toFixed(1)+"Cr";if(Math.abs(v)>=100000)return(v/100000).toFixed(1)+"L";if(Math.abs(v)>=1000)return(v/1000).toFixed(1)+"K";return v.toLocaleString("en-IN",{maximumFractionDigits:2});}return String(v);}function mkTbl(data,cols){if(!data||!data.length)return "<p style='color:#9ca3af;font-size:11px'>No rows returned</p>";var h="<div class='rw'><table><thead><tr>";cols.forEach(function(c){h+="<th>"+c+"</th>";});h+="</tr></thead><tbody>";data.slice(0,500).forEach(function(row){h+="<tr>";cols.forEach(function(c){var v=row[c],n=typeof v==="number";h+="<td style='"+(n?"text-align:right;font-family:monospace":"")+">"+fmt(v)+"</td>";});h+="</tr>";});return h+"</tbody></table></div>";}function mkChart(data,cols,type,cfg,id){if(!type||type==="none"||type==="table"||!data||!data.length)return "";var xC=cfg&&cfg.x_axis?cfg.x_axis:cols[0],yC=cfg&&cfg.y_axis?cfg.y_axis:cols[1];setTimeout(function(){var ctx=document.getElementById(id);if(!ctx)return;if(charts[id]){charts[id].destroy();}var lbls=data.slice(0,30).map(function(r){return String(r[xC]||"").slice(0,25);});var vals=data.slice(0,30).map(function(r){return parseFloat(r[yC])||0;});var clrs=vals.map(function(_,i){return "hsla("+(200+i*8)+",65%,45%,.8)";});charts[id]=new Chart(ctx.getContext("2d"),{type:type==="lin"?"line":type,data:{labels:lbls,datasets:[{label:yC,data:vals,backgroundColor:clrs,borderColor:clrs,borderWidth:type==="line"?2:1,tension:.4,pointRadius:3}]},options:{responsive:true,maintainAspectRatio:false,plugins:{legend:{display:false}},scales:{y:{ticks:{callback:function(v){return fmt(v);}}}}}});},100);return "<div class='lbl'>CHART</div><div class='chart-c'><canvas id='"+id+"' height='250'></canvas></div>";}async function sendQ(){var q=qEl.value.trim();if(!q)return;btn.disabled=true;qEl.value="";document.getElementById("sugs").style.display="none";addMsg("<div class='bu'>"+q+"</div>","user");var ld=addMsg("<div class='ba'><div class='ld'><span></span><span></span><span></span></div></div>","ai");chatHistory.push({role:"user",content:q});try{var res=await fetch("/ask",{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({question:q,history:chatHistory.slice(-6)})});var d=await res.json();ld.remove();var cid="c"+Date.now();var html="<div class='ba'>";if(d.explanation)html+="<div class='expl'>"+d.explanation+"</div>";if(d.sql)html+="<div class='lbl'>SQL</div><div class='sql-b'>"+d.sql+"</div>";if(d.result&&d.result.rows&&d.result.rows.length){var cols=Object.keys(d.result.rows[0]);html+="<div class='lbl'>RESULT &mdash; "+d.result.count+" rows</div>";html+=mkTbl(d.result.rows,cols);html+=mkChart(d.result.rows,cols,d.chart_type,d.chart_config,cid);}if(d.error&&!d.result)html+="<div class='err'>"+d.error+"</div>";if(d.follow_up&&d.follow_up.length){html+="<div class='fups'>";d.follow_up.forEach(function(f){html+="<span class='fup' onclick='qEl.value=this.textContent;sendQ();'>"+f+"</span>";});html+="</div>";}html+="</div>";addMsg(html,"ai");chatHistory.push({role:"assistant",content:d.explanation||"Done"});}catch(e){ld.remove();addMsg("<div class='ba'><div class='err'>Error: "+e.message+"</div></div>","ai");}btn.disabled=false;}</script></body></html>`;

    return new Response(HTML,{headers:{...CORS_H,"Content-Type":"text/html;charset=UTF-8"}});
  }
};
