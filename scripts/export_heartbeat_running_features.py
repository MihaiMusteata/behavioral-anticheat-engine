#!/usr/bin/env python3
"""Builds a timeseries dataset where each state_heartbeat row is enriched
with RUNNING (cumulative, as-of-that-moment) counts of every other event
type that had already occurred in the same session - not the session's
final totals.

This is deliberately NOT a join against session_features.csv: using final
totals at every timestep would leak future information backward (at
heartbeat #2 you'd already see copy counts from events that hadn't
happened yet), which would invalidate any timeseries model trained on it.
Instead this script replays each session's events in chronological order
and snapshots the running counts at every heartbeat.

Usage:
    python export_heartbeat_running_features.py
"""

import csv
import json
from collections import defaultdict
from pathlib import Path

import psycopg2.extras

from db import get_connection

OUTPUT_PATH = Path(__file__).resolve().parents[1] / "datasets" / "heartbeat_running_features.csv"

QUERY = """
    SELECT
        "SessionId" AS session_id,
        "Seq" AS seq,
        "TimestampUtc" AS timestamp_utc,
        "EventType" AS event_type,
        "PayloadJson" AS payload_json
    FROM behavioral_events
    ORDER BY "SessionId", "TimestampUtc", "Seq";
"""

HEARTBEAT_PAYLOAD_FIELDS = [
    "idleMs",
    "mouseX",
    "mouseY",
    "scrollY",
    "windowGapWidth",
    "windowGapHeight",
    "questionId",
    "questionIndex",
]


def parse_json_field(value):
    # psycopg2 auto-deserializes jsonb columns into dicts already; this
    # only falls back to json.loads for drivers/configs that leave it as text.
    if value is None:
        return {}
    return value if isinstance(value, dict) else json.loads(value)


def main():
    with get_connection() as conn:
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
            cursor.execute(QUERY)
            rows = cursor.fetchall()

    if not rows:
        print("No events found - nothing to export.")
        return

    # Columns are fixed up front from every event type seen anywhere, so
    # the CSV shape is stable across sessions even when a given session
    # never triggered some event type (it just stays 0 for that session).
    event_types = sorted({row["event_type"] for row in rows})
    count_columns = [f"{event_type}_count_so_far" for event_type in event_types]
    fieldnames = ["session_id", "seq", "timestamp_utc"] + HEARTBEAT_PAYLOAD_FIELDS + count_columns

    output_rows = []
    running_counts = defaultdict(int)
    current_session = None

    for row in rows:
        if row["session_id"] != current_session:
            running_counts = defaultdict(int)
            current_session = row["session_id"]

        if row["event_type"] == "state_heartbeat":
            payload = parse_json_field(row["payload_json"])
            output_row = {
                "session_id": row["session_id"],
                "seq": row["seq"],
                "timestamp_utc": row["timestamp_utc"],
            }
            for field in HEARTBEAT_PAYLOAD_FIELDS:
                output_row[field] = payload.get(field)
            for event_type, column in zip(event_types, count_columns):
                output_row[column] = running_counts[event_type]
            output_rows.append(output_row)

        # Counted AFTER the snapshot above, so a heartbeat's own row never
        # includes itself (or anything at/after its timestamp) in the counts.
        running_counts[row["event_type"]] += 1

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_PATH.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(output_rows)

    print(f"Wrote {len(output_rows)} heartbeat rows (with running feature counts) to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
