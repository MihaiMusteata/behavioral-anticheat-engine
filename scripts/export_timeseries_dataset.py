#!/usr/bin/env python3
"""Builds the per-session timeseries dataset used for timeseries algorithms
(e.g. ARIMA): one row per state_heartbeat sample, long/panel format (many
rows per session, ordered by time), sampled at a fixed ~8 second interval -
unlike export_session_dataset.py, this is NOT one row per session.

Usage:
    python export_timeseries_dataset.py
"""

import csv
import json
from pathlib import Path

import psycopg2.extras

from db import get_connection


def parse_json_field(value):
    # psycopg2 auto-deserializes jsonb columns into dicts already; this
    # only falls back to json.loads for drivers/configs that leave it as text.
    if value is None:
        return {}
    return value if isinstance(value, dict) else json.loads(value)

OUTPUT_PATH = Path(__file__).resolve().parents[1] / "datasets" / "heartbeat_timeseries.csv"

QUERY = """
    SELECT
        "SessionId" AS session_id,
        "Seq" AS seq,
        "TimestampUtc" AS timestamp_utc,
        "PayloadJson" AS payload_json
    FROM behavioral_events
    WHERE "EventType" = 'state_heartbeat'
    ORDER BY "SessionId", "TimestampUtc";
"""

# Keys written into the state_heartbeat payload by StateHeartbeatDetector
# (client/src/features/behavior/detectors.ts), plus the question context
# that the collector merges into every event automatically.
PAYLOAD_FIELDS = [
    "idleMs",
    "mouseX",
    "mouseY",
    "scrollY",
    "windowGapWidth",
    "windowGapHeight",
    "questionId",
    "questionIndex",
]


def main():
    with get_connection() as conn:
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
            cursor.execute(QUERY)
            rows = cursor.fetchall()

    if not rows:
        print("No heartbeat events found - nothing to export.")
        return

    fieldnames = ["session_id", "seq", "timestamp_utc"] + PAYLOAD_FIELDS

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_PATH.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            payload = parse_json_field(row.pop("payload_json"))
            for field in PAYLOAD_FIELDS:
                row[field] = payload.get(field)
            writer.writerow(row)

    print(f"Wrote {len(rows)} heartbeat samples to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
