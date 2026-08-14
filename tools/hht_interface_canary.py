#!/usr/bin/env python3
"""
HHT function-module interface canary.

WHY THIS EXISTS
---------------
On 2026-08-13 a transport added IM_ZONE to ZWM_PTL_GRT_HUB_CRATE_VLDT. It worked in
DEV and arrived blank in QA. Root cause was not SAP: the SAP .NET Connector caches an
FM's interface per destination, the cached copy still had the old 6 parameters, the
proxy dropped IM_ZONE and invoked the RFC anyway. A day was spent chasing a phantom
SAP bug; an app-pool recycle cleared it.

rfc-api now self-heals that (see GenericRfcProxyController). The PROD HHT path does
NOT go through rfc-api — it goes Azure -> Hybrid Connection -> 192.168.144.200:9080
Java middleware -> SAP PROD. That middleware uses SAP JCo, and a decompile of
xmwgw.war shows 506 JCoRepository references and ZERO calls to
removeFunctionTemplateFromCache / clearFunctionTemplates. It has the identical stale
cache, and it is worse: RFCExecutionAdaptor.configureInputParams iterates the SAP-side
JCoParameterFieldIterator and pulls matching keys out of the JSON, so a parameter the
cached template does not know about is skipped in total silence — no error, no echo of
what was applied. Restarting the Azure app does not clear it; only a Tomcat restart on
.200 does, and that box is not remotely administrable from CI.

So PROD cannot self-heal and cannot report the failure. This canary makes the failure
LOUD instead: it re-reads every HHT function module's live interface from SAP PROD and
fails the build the moment one changes. A change means a transport landed, which means
the Java MW's cached template is now stale, which means the new parameter is silently
arriving blank on ~1000 devices until someone restarts Tomcat on .200.

HOW IT READS GROUND TRUTH
-------------------------
rfc-api's /api/rfc/refresh?env=prod&fm=X drops that FM from the NCo cache on .36 and
re-reads it from SAP, returning the live interface. Clearing is the point: it is what
makes the answer ground truth rather than another cached copy. It is also harmless —
the next proxy call re-reads the metadata in about a second.

USAGE
-----
  python hht_interface_canary.py --snapshot     # rewrite the baseline (after an approved change)
  python hht_interface_canary.py                # check against the baseline; exit 1 on drift
"""

import argparse
import json
import os
import sys
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor

BASE = os.environ.get("RFC_API_BASE", "https://sap-api.v2retail.net")
KEY = os.environ.get("RFC_API_KEY", "v2-rfc-proxy-2026")
ENV = os.environ.get("RFC_ENV", "prod")

HERE = os.path.dirname(os.path.abspath(__file__))
WATCHLIST = os.path.join(HERE, "hht_fm_watchlist.txt")
BASELINE = os.path.join(HERE, "hht_fm_interfaces.prod.json")

# A few FMs at a time. This is a live production SAP system fronting 320+ stores;
# 233 metadata reads is trivial load, 233 simultaneous ones is not polite.
WORKERS = 6
TIMEOUT = 45


def read_interface(fm):
    """Return (fm, signature) where signature is a sorted list of 'NAME:DIR:TYPE:LEN'.

    An FM that does not exist in this environment returns the sentinel "__ABSENT__".
    That is a legitimate state, not an error: the DEV/QA-only function modules in the
    watchlist have simply not been transported to production yet. Their arrival is
    itself a change worth flagging.
    """
    url = "%s/api/rfc/refresh?env=%s&fm=%s" % (BASE, ENV, fm)
    # sap-api.v2retail.net sits behind Cloudflare, which answers the stock
    # "Python-urllib/3.x" agent with 403 error 1010 before the request ever reaches IIS.
    req = urllib.request.Request(url, method="POST", headers={
        "X-RFC-Key": KEY,
        "User-Agent": "v2-hht-interface-canary/1.0",
    })
    try:
        with urllib.request.urlopen(req, timeout=TIMEOUT) as resp:
            data = json.loads(resp.read().decode("utf-8", "replace"))
    except urllib.error.HTTPError as e:
        body = e.read().decode("utf-8", "replace")[:200]
        return fm, ["__ERROR__:HTTP %d %s" % (e.code, body)]
    except Exception as e:
        return fm, ["__ERROR__:%s" % e]

    iface = data.get("_INTERFACE")
    if iface == []:
        # A real FM with a genuinely empty interface. Kept as a signature of its own so
        # that gaining a parameter later still registers as drift.
        return fm, ["__NO_PARAMS__"]
    if iface is None:
        msg = (data.get("EX_RETURN") or {}).get("MESSAGE", "")
        # NCo says "not found in SAP" for an FM that has not been transported here.
        if "not found" in msg.lower() or "does not exist" in msg.lower():
            return fm, ["__ABSENT__"]
        return fm, ["__ERROR__:%s" % (msg or json.dumps(data)[:200])]

    return fm, sorted(
        "%s:%s:%s:%s" % (p.get("name"), p.get("direction"), p.get("type"), p.get("length"))
        for p in iface
    )


def load_watchlist():
    with open(WATCHLIST, "r", encoding="utf-8") as fh:
        return [
            ln.strip()
            for ln in fh
            if ln.strip() and not ln.startswith("#")
        ]


def collect(fms):
    out = {}
    with ThreadPoolExecutor(max_workers=WORKERS) as pool:
        for fm, sig in pool.map(read_interface, fms):
            out[fm] = sig
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--snapshot", action="store_true",
                    help="rewrite the baseline instead of checking against it")
    args = ap.parse_args()

    fms = load_watchlist()
    print("Reading %d function-module interfaces live from SAP %s..." % (len(fms), ENV.upper()))
    current = collect(fms)

    errors = {k: v for k, v in current.items() if v and v[0].startswith("__ERROR__")}

    if args.snapshot:
        # Never bake a transport error into the baseline — it would mask the next real
        # change behind a permanently "expected" error string.
        if errors:
            print("\nRefusing to snapshot: %d function module(s) could not be read." % len(errors))
            for k, v in sorted(errors.items())[:10]:
                print("  %-36s %s" % (k, v[0]))
            return 1
        with open(BASELINE, "w", encoding="utf-8") as fh:
            json.dump(current, fh, indent=1, sort_keys=True)
            fh.write("\n")
        present = sum(1 for v in current.values() if v != ["__ABSENT__"])
        print("Baseline written: %d FMs (%d live in %s, %d not yet transported)."
              % (len(current), present, ENV.upper(), len(current) - present))
        return 0

    with open(BASELINE, "r", encoding="utf-8") as fh:
        baseline = json.load(fh)

    added, removed, changed = [], [], []
    for fm in sorted(set(baseline) | set(current)):
        was, now = baseline.get(fm), current.get(fm)
        if now and now[0].startswith("__ERROR__"):
            continue  # reported separately; a transient read failure is not drift
        if was is None:
            added.append(fm)
        elif now is None:
            removed.append(fm)
        elif was != now:
            changed.append((fm, was, now))

    if errors:
        print("\n%d function module(s) could not be read (not treated as drift):" % len(errors))
        for k, v in sorted(errors.items()):
            print("  %-36s %s" % (k, v[0][:120]))

    if not (added or removed or changed):
        print("No interface drift. PROD Java MW cache is consistent with SAP %s." % ENV.upper())
        return 0

    print("\n" + "=" * 78)
    print("INTERFACE DRIFT DETECTED - the Java MW on 192.168.144.200 is now STALE")
    print("=" * 78)

    for fm, was, now in changed:
        was_set, now_set = set(was), set(now)
        print("\n%s" % fm)
        for p in sorted(now_set - was_set):
            print("   + %s" % p)
        for p in sorted(was_set - now_set):
            print("   - %s" % p)

    for fm in added:
        print("\n%s  (newly watched)" % fm)
    for fm in removed:
        print("\n%s  (no longer watched)" % fm)

    print("""
WHAT THIS MEANS
  A transport changed one of these interfaces in SAP PROD. The Java middleware on
  192.168.144.200:9080 still holds the OLD JCo function template. Any parameter added
  above is being dropped on the floor right now - SAP receives spaces, the RFC runs
  anyway, and neither the device nor the log says a word.

WHAT TO DO
  1. Restart Tomcat on 192.168.144.200 (this is the only thing that clears the JCo
     template cache - restarting the Azure app v2-hht-api does NOT).
  2. Re-run the affected HHT flow end to end and confirm the new parameter arrives.
  3. Re-baseline:  python tools/hht_interface_canary.py --snapshot
     and commit tools/hht_fm_interfaces.prod.json.

  Background: v2retail/HHT QA IM_ZONE Blank - NCo Metadata Cache 2026-08-13
""")
    return 1


if __name__ == "__main__":
    sys.exit(main())
