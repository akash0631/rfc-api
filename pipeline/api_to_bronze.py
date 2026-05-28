#!/usr/bin/env python3
"""
api_to_bronze.py — pull from sap-api.v2retail.net /api/* and land in
V2RETAIL.BRONZE.RFC_API_* tables.

Runs daily from .github/workflows/api-to-bronze-sync.yml.
Standalone — does not touch GOLD.RFC_MASTER or any ET_* table.

Endpoints loaded (each respects its own window cap + page size):
  /api/po              -> BRONZE.RFC_API_PO
  /api/grn             -> BRONZE.RFC_API_GRN
  /api/sales           -> BRONZE.RFC_API_SALES
  /api/articles        -> BRONZE.RFC_API_ARTICLES
  /api/vendors         -> BRONZE.RFC_API_VENDORS
  /api/plants          -> BRONZE.RFC_API_PLANTS
  /api/article-colors  -> BRONZE.RFC_API_ARTICLE_COLORS

Env vars required:
  RFC_API_BASE          https://sap-api.v2retail.net
  RFC_API_KEY           v2-rfc-proxy-2026
  RFC_API_ENV           prod | qa | dev   (default prod)
  SF_ACCOUNT            iafphkw-hh80816
  SF_USER               POWERBI            (or service user)
  SF_ROLE               ACCOUNTADMIN       (or scoped role)
  SF_WAREHOUSE          COMPUTE_WH
  SF_PRIVATE_KEY_B64    base64-encoded PKCS#8 PEM
  SF_PRIVATE_KEY_PWD    (optional, if key is encrypted)

Optional:
  DATE_FROM             YYYY-MM-DD (default yesterday UTC)
  DATE_TO               YYYY-MM-DD (default DATE_FROM)
  ENDPOINTS             CSV of names to load (default: all)
  PAGE_SIZE             default 5000
  GITHUB_RUN_URL        passed in by workflow for audit trail
"""

from __future__ import annotations

import base64
import json
import os
import sys
import time
import uuid
from datetime import datetime, timedelta, timezone
from typing import Any, Dict, Iterable, List, Optional, Tuple

import requests
import snowflake.connector
from cryptography.hazmat.backends import default_backend
from cryptography.hazmat.primitives import serialization

API_BASE = os.environ.get("RFC_API_BASE", "https://sap-api.v2retail.net").rstrip("/")
API_KEY = os.environ["RFC_API_KEY"]
API_ENV = os.environ.get("RFC_API_ENV", "prod")
PAGE_SIZE = int(os.environ.get("PAGE_SIZE", "5000"))
LOAD_ID = os.environ.get("GITHUB_RUN_ID") or str(uuid.uuid4())
RUN_URL = os.environ.get("GITHUB_RUN_URL", "")

session = requests.Session()
session.headers.update({"X-RFC-Key": API_KEY})


def yesterday_utc() -> str:
    return (datetime.now(timezone.utc) - timedelta(days=1)).strftime("%Y-%m-%d")


DATE_FROM = os.environ.get("DATE_FROM") or yesterday_utc()
DATE_TO = os.environ.get("DATE_TO") or DATE_FROM


# ── Endpoint catalog ─────────────────────────────────────────────────────────
# Each row: (endpoint, target_table, has_date, page_size_override, mapper)
# mapper(row_dict) -> tuple matching INSERT column order


def map_po(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("PurchasingDoc"), r.get("PoType"),
        r.get("CreatedOn") or None, r.get("CreatedBy"),
        r.get("Supplier"), _num(r.get("NetValue")), _num(r.get("PoQuantity")),
        r.get("Plant"),
    )


PO_COLS = "PURCHASING_DOC, PO_TYPE, CREATED_ON, CREATED_BY, SUPPLIER, NET_VALUE, PO_QUANTITY, PLANT"


def map_grn(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("MaterialDoc"), r.get("Year"), r.get("MovementType"),
        r.get("Plant"), r.get("Supplier"), r.get("DebitCredit"),
        _num(r.get("AmountInLC")), _num(r.get("Quantity")), r.get("BaseUnit"),
        r.get("PurchaseOrder"), r.get("ReferenceDoc"), r.get("SupplierReceive"),
        r.get("TransEvType"),
        r.get("PostingDate") or None, r.get("EnteredOn") or None,
        r.get("Text"), r.get("MovementWM"),
    )


GRN_COLS = ("MATERIAL_DOC, MAT_DOC_YEAR, MOVEMENT_TYPE, PLANT, SUPPLIER, DEBIT_CREDIT, "
            "AMOUNT_IN_LC, QUANTITY, BASE_UNIT, PURCHASE_ORDER, REFERENCE_DOC, "
            "SUPPLIER_RECEIVE, TRANS_EV_TYPE, POSTING_DATE, ENTERED_ON, TEXT, MOVEMENT_WM")


def map_sales(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("VBELN"), r.get("POSNR"), r.get("FKDAT") or None,
        r.get("WERKS"), r.get("LGORT"), r.get("MATNR"),
        _num(r.get("FKIMG")), r.get("VRKME"), r.get("WAERK"),
        _num(r.get("NETWR")), _num(r.get("KZWI1")), _num(r.get("KZWI2")),
        _num(r.get("MWSBP")), _num(r.get("WAVWR")), _num(r.get("NET_VAL")),
        _num(r.get("VKP0")), _num(r.get("KWERT_VPRS")), _num(r.get("KBETR_VPRS")),
    )


SALES_COLS = ("VBELN, POSNR, FKDAT, WERKS, LGORT, MATNR, FKIMG, VRKME, WAERK, "
              "NETWR, KZWI1, KZWI2, MWSBP, WAVWR, NET_VAL, VKP0, KWERT_VPRS, KBETR_VPRS")


def map_article(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("MATNR"), r.get("ERSDA") or None, r.get("MTART"),
        r.get("MATKL"), r.get("MEINS"), json.dumps(r),
    )


ARTICLES_COLS = "MATNR, ERSDA, MTART, MATKL, MEINS, PAYLOAD"


def map_vendor(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("LIFNR"), r.get("NAME1"), r.get("ORT01"), r.get("REGIO"),
        r.get("STRAS"), r.get("ERDAT") or None, r.get("KTOKK"),
        r.get("TELF1"), r.get("SMTP_ADDR"), r.get("ZTERM"), r.get("TAXNUM"),
        json.dumps(r),
    )


VENDORS_COLS = ("LIFNR, NAME1, ORT01, REGIO, STRAS, ERDAT, KTOKK, "
                "TELF1, SMTP_ADDR, ZTERM, TAXNUM, PAYLOAD")


def map_plant(r: Dict[str, Any]) -> Tuple:
    return (
        r.get("__Type"), r.get("WERKS"), r.get("NAME1"), r.get("LAND1"),
        r.get("REGIO"), r.get("ORT01"), r.get("PSTLZ"), r.get("STRAS"),
        r.get("LGORT"), json.dumps(r),
    )


PLANTS_COLS = "ROW_TYPE, WERKS, NAME1, LAND1, REGIO, ORT01, PSTLZ, STRAS, LGORT, PAYLOAD"


def map_color(r: Dict[str, Any]) -> Tuple:
    return (r.get("MATNR"), r.get("COLOR"), r.get("ATWTB"))


COLORS_COLS = "MATNR, COLOR, ATWTB"


ENDPOINTS = {
    "po":             dict(path="/api/po",             table="RFC_API_PO",            has_date=True,  cols=PO_COLS,       mapper=map_po),
    "grn":            dict(path="/api/grn",            table="RFC_API_GRN",           has_date=True,  cols=GRN_COLS,      mapper=map_grn),
    "sales":          dict(path="/api/sales",          table="RFC_API_SALES",         has_date=True,  cols=SALES_COLS,    mapper=map_sales),
    "articles":       dict(path="/api/articles",       table="RFC_API_ARTICLES",      has_date=True,  cols=ARTICLES_COLS, mapper=map_article),
    "vendors":        dict(path="/api/vendors",        table="RFC_API_VENDORS",       has_date=True,  cols=VENDORS_COLS,  mapper=map_vendor),
    "plants":         dict(path="/api/plants",         table="RFC_API_PLANTS",        has_date=False, cols=PLANTS_COLS,   mapper=map_plant),
    "article-colors": dict(path="/api/article-colors", table="RFC_API_ARTICLE_COLORS", has_date=True, cols=COLORS_COLS,  mapper=map_color),
}


# ── Helpers ──────────────────────────────────────────────────────────────────


def _num(v: Any) -> Optional[float]:
    if v is None or v == "":
        return None
    try:
        return float(v)
    except (ValueError, TypeError):
        return None


def fetch_pages(path: str, has_date: bool) -> Iterable[Dict[str, Any]]:
    """Paginate the /api/* endpoint, yielding each row dict."""
    offset = 0
    total: Optional[int] = None
    while True:
        params = {"Offset": offset, "Limit": PAGE_SIZE, "env": API_ENV}
        if has_date:
            params["DateFrom"] = DATE_FROM
            params["DateTo"] = DATE_TO
        r = session.get(f"{API_BASE}{path}", params=params, timeout=300)
        r.raise_for_status()
        body = r.json()
        if not body.get("Success", False) and not body.get("Rows"):
            raise RuntimeError(f"{path} returned Success=false: {body.get('Error') or body.get('SapMessage')}")
        rows = body.get("Rows") or []
        total = body.get("TotalRows", total)
        for row in rows:
            yield row
        if not body.get("HasMore"):
            break
        offset = body["NextOffset"]
    print(f"   -- {path} total={total} pulled={offset + len(rows) if total else '?'}")


def sf_connect() -> snowflake.connector.SnowflakeConnection:
    pk_b64 = os.environ["SF_PRIVATE_KEY_B64"]
    pwd = os.environ.get("SF_PRIVATE_KEY_PWD")
    pk_pem = base64.b64decode(pk_b64)
    pk = serialization.load_pem_private_key(
        pk_pem,
        password=pwd.encode() if pwd else None,
        backend=default_backend(),
    )
    pk_der = pk.private_bytes(
        encoding=serialization.Encoding.DER,
        format=serialization.PrivateFormat.PKCS8,
        encryption_algorithm=serialization.NoEncryption(),
    )
    return snowflake.connector.connect(
        account=os.environ["SF_ACCOUNT"],
        user=os.environ["SF_USER"],
        role=os.environ.get("SF_ROLE", "ACCOUNTADMIN"),
        warehouse=os.environ.get("SF_WAREHOUSE", "COMPUTE_WH"),
        database=os.environ.get("SF_DATABASE", "V2RETAIL"),
        schema=os.environ.get("SF_SCHEMA", "BRONZE"),
        private_key=pk_der,
    )


def insert_batch(cur, table: str, cols: str, rows: List[Tuple]) -> int:
    if not rows:
        return 0
    placeholders = ", ".join(["%s"] * (len(cols.split(",")) + 4))  # + 4 meta cols
    sql = f"INSERT INTO {table} ({cols}, _LOAD_ID, _LOAD_TS, _SOURCE, _ENV) VALUES ({placeholders})"
    now_utc = datetime.now(timezone.utc).replace(tzinfo=None)
    rows_with_meta = [r + (LOAD_ID, now_utc, table, API_ENV) for r in rows]
    cur.executemany(sql, rows_with_meta)
    return len(rows_with_meta)


def log_run(cur, ep: str, started: datetime, rows_api: int, rows_inserted: int,
            status: str, err: str = "") -> None:
    cur.execute(
        """INSERT INTO RFC_API_SYNC_LOG
           (LOAD_ID, ENDPOINT, ENV, DATE_FROM, DATE_TO, STARTED_AT, ENDED_AT,
            ROWS_FROM_API, ROWS_INSERTED, STATUS, ERROR_TEXT, GITHUB_RUN_URL)
           VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)""",
        (LOAD_ID, ep, API_ENV, DATE_FROM, DATE_TO,
         started.replace(tzinfo=None), datetime.utcnow(),
         rows_api, rows_inserted, status, err[:1000], RUN_URL),
    )


def load_endpoint(conn, ep: str, cfg: Dict[str, Any]) -> Tuple[int, int]:
    started = datetime.now(timezone.utc)
    cur = conn.cursor()
    try:
        rows: List[Tuple] = []
        api_count = 0
        for row in fetch_pages(cfg["path"], cfg["has_date"]):
            rows.append(cfg["mapper"](row))
            api_count += 1
            if len(rows) >= 5000:
                insert_batch(cur, cfg["table"], cfg["cols"], rows)
                rows.clear()
        inserted = api_count
        if rows:
            insert_batch(cur, cfg["table"], cfg["cols"], rows)
        log_run(cur, ep, started, api_count, inserted, "OK")
        conn.commit()
        return api_count, inserted
    except Exception as e:
        conn.rollback()
        log_run(cur, ep, started, 0, 0, "ERROR", str(e))
        conn.commit()
        raise
    finally:
        cur.close()


def main() -> int:
    print(f"[api_to_bronze] LOAD_ID={LOAD_ID} ENV={API_ENV} {DATE_FROM} -> {DATE_TO}")
    wanted = os.environ.get("ENDPOINTS")
    selected = [e.strip() for e in wanted.split(",")] if wanted else list(ENDPOINTS.keys())
    bad = [e for e in selected if e not in ENDPOINTS]
    if bad:
        print(f"unknown endpoints: {bad}", file=sys.stderr)
        return 2

    conn = sf_connect()
    print(f"[api_to_bronze] connected to {os.environ['SF_ACCOUNT']}")

    overall_failed = False
    for ep in selected:
        cfg = ENDPOINTS[ep]
        t0 = time.time()
        try:
            api_n, ins_n = load_endpoint(conn, ep, cfg)
            print(f"   OK {ep:18s} api={api_n:>7d} inserted={ins_n:>7d} {time.time()-t0:5.1f}s")
        except Exception as e:
            overall_failed = True
            print(f"   XX {ep:18s} FAILED after {time.time()-t0:.1f}s — {e}")

    conn.close()
    return 1 if overall_failed else 0


if __name__ == "__main__":
    sys.exit(main())
