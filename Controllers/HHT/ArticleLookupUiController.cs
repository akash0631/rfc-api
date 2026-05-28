using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web.Http;

namespace Vendor_Application_MVC.Controllers.HHT
{
    // GET /api/article-lookup-ui  -> tiny HTML test page that calls /api/article-lookup
    public class ArticleLookupUiController : ApiController
    {
        [HttpGet, Route("api/article-lookup-ui")]
        public HttpResponseMessage Ui()
        {
            var r = Request.CreateResponse(HttpStatusCode.OK);
            r.Content = new StringContent(HTML, Encoding.UTF8);
            r.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html") { CharSet = "utf-8" };
            return r;
        }

        private const string HTML = @"<!doctype html>
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>Article Lookup — Test</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body { font: 16px/1.4 system-ui, -apple-system, Segoe UI, sans-serif; max-width: 480px; margin: 24px auto; padding: 0 16px; }
  h1 { font-size: 20px; margin: 0 0 16px; }
  label { display: block; font-size: 13px; color: #555; margin: 12px 0 4px; }
  input, button, select { font: inherit; width: 100%; padding: 10px 12px; border: 1px solid #bbb; border-radius: 8px; }
  button { background: #1a73e8; color: #fff; border-color: #1a73e8; margin-top: 16px; cursor: pointer; }
  button:disabled { opacity: .5; cursor: wait; }
  .row { display: flex; gap: 8px; }
  .row > * { flex: 1; }
  .card { border: 1px solid #ddd; border-radius: 12px; padding: 16px; margin-top: 20px; background: #fafafa; }
  .fields { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 12px; }
  .field { background: #fff; border: 1px solid #e3e3e3; border-radius: 10px; padding: 10px 12px; }
  .lbl { font-size: 11px; color: #888; text-transform: uppercase; letter-spacing: .5px; }
  .val { font-size: 24px; font-weight: 600; margin-top: 4px; }
  .val.C  { color: #b00020; }
  .val.L  { color: #0a7f2e; }
  .val.RL { color: #0a7f2e; }
  .val.NL { color: #888; }
  .val.BS { color: #1a73e8; }
  .val.S  { color: #555; }
  .val.NORMAL { color: #555; }
  .meta { font-size: 12px; color: #888; margin-top: 12px; }
  .err  { color: #b00020; background: #fdecea; border: 1px solid #f5c2c0; border-radius: 8px; padding: 10px 12px; margin-top: 16px; }
  .placeholder { color: #bbb; }
  details { margin-top: 12px; font-size: 13px; }
  pre { background: #f4f4f4; padding: 8px; border-radius: 6px; overflow: auto; font-size: 12px; }
</style>
</head>
<body>

<h1>Article Lookup — HHT test</h1>
<p style=""font-size:13px;color:#666;margin:0 0 8px"">V09 → 0001 Article Putaway. Scan barcode or paste manually.</p>

<form id=""f"">
  <label for=""store"">Store</label>
  <input id=""store"" autocomplete=""off"" value=""HA11"" required>

  <label for=""article"">Article (13-digit variant)</label>
  <input id=""article"" autocomplete=""off"" inputmode=""numeric"" placeholder=""scan or type"" autofocus required>

  <button id=""go"" type=""submit"">Lookup</button>
</form>

<div id=""out""></div>

<details>
  <summary>Quick samples</summary>
  <ul style=""line-height:1.8"">
    <li><a href=""#"" data-s=""HA11"" data-a=""1125011967001"">HA11 / 1125011967001 → L / S</a></li>
    <li><a href=""#"" data-s=""HA10"" data-a=""1114091884004"">HA10 / 1114091884004 → RL / NORMAL</a></li>
    <li><a href=""#"" data-s=""HB06"" data-a=""1123117012004"">HB06 / 1123117012004 → NL / NORMAL</a></li>
    <li><a href=""#"" data-s=""HA11"" data-a=""1220006246001"">HA11 / 1220006246001 → C / NORMAL</a></li>
    <li><a href=""#"" data-s=""HA11"" data-a=""1130139636004"">HA11 / 1130139636004 → L / BS</a></li>
  </ul>
</details>

<script>
const $ = id => document.getElementById(id);
const out = $('out');
const f   = $('f');

function render(state, data, ms) {
  if (state === 'loading') {
    out.innerHTML = '<div class=""card""><div class=""placeholder"">Looking up…</div></div>';
    return;
  }
  if (state === 'error') {
    out.innerHTML = '<div class=""err"">' + escapeHtml(data) + '</div>';
    return;
  }
  const t = data.article_type || '—';
  const s = data.article_size || '—';
  const okBadge  = data.status === true ? '✓ live' : '⚠ fallback';
  const msg = data.message ? ('<div class=""err"" style=""margin-top:10px"">' + escapeHtml(data.message) + '</div>') : '';
  out.innerHTML =
    '<div class=""card"">' +
      '<div class=""meta"">Store <b>' + escapeHtml(data.store) + '</b> · Article <b>' + escapeHtml(data.article) + '</b> · ' + okBadge + ' · ' + (data.ms ?? ms) + ' ms</div>' +
      '<div class=""fields"">' +
        '<div class=""field""><div class=""lbl"">Article Type</div><div class=""val ' + escapeAttr(t) + '"">' + escapeHtml(t) + '</div></div>' +
        '<div class=""field""><div class=""lbl"">Article Size</div><div class=""val ' + escapeAttr(s) + '"">' + escapeHtml(s) + '</div></div>' +
      '</div>' +
      msg +
    '</div>';
}

function escapeHtml(x) {
  return String(x ?? '').replace(/[&<>""']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[c]));
}
function escapeAttr(x) { return String(x ?? '').replace(/[^A-Za-z0-9_-]/g, ''); }

async function lookup(store, article) {
  render('loading');
  const t0 = performance.now();
  try {
    const res = await fetch('/api/article-lookup', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ store, article })
    });
    const data = await res.json();
    const ms = Math.round(performance.now() - t0);
    if (!res.ok && !data.status) { render('error', (data.message || ('HTTP ' + res.status))); return; }
    render('ok', data, ms);
  } catch (e) {
    render('error', e.message || String(e));
  }
}

f.addEventListener('submit', e => {
  e.preventDefault();
  const store   = $('store').value.trim();
  const article = $('article').value.trim();
  if (!store || !article) return;
  lookup(store, article);
});

// Auto-warm the store cache once the operator commits a store value, so the
// first barcode scan is fast. Idempotent on the server.
let warmedStore = null;
async function maybeWarm() {
  const store = $('store').value.trim().toUpperCase();
  if (!store || store === warmedStore) return;
  warmedStore = store;
  try { await fetch('/api/article-lookup/warm', { method:'POST', headers:{'Content-Type':'application/json'}, body: JSON.stringify({ store }) }); } catch {}
}
$('store').addEventListener('blur', maybeWarm);
$('store').addEventListener('change', maybeWarm);
window.addEventListener('load', maybeWarm);

// HHT barcode scanners send the scan + Enter; submit auto-fires.
// Auto-clear article field after a successful lookup so next scan replaces it.
out.addEventListener('animationend', () => {});
document.querySelectorAll('details a[data-a]').forEach(a => {
  a.addEventListener('click', e => {
    e.preventDefault();
    $('store').value   = a.dataset.s;
    $('article').value = a.dataset.a;
    lookup(a.dataset.s, a.dataset.a);
  });
});
</script>
</body></html>";
    }
}
