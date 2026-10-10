#!/usr/bin/env python3
"""Builds the session-level dataset (one row per exam session) used for
classification and regression: aggregated behavioral features, already
computed server-side in session_feature_aggregates, joined with each
session's outcome fields from exam_sessions (notably self_reported_cheated,
the closest thing this system has to a ground-truth label).

Usage:
    python export_session_dataset.py
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

OUTPUT_PATH = Path(__file__).resolve().parents[1] / "datasets" / "session_features.csv"

QUERY = """
    SELECT
        sfa."SessionId" AS session_id,
        sfa."ExamScheduleId" AS exam_schedule_id,
        sfa."StudentId" AS student_id,
        sfa."TotalEvents" AS total_events,
        sfa."FocusLostCount" AS focus_lost_count,
        sfa."VisibilityHiddenCount" AS visibility_hidden_count,
        sfa."CopyCount" AS copy_count,
        sfa."PasteCount" AS paste_count,
        sfa."FullscreenExitCount" AS fullscreen_exit_count,
        sfa."DevtoolsSignals" AS devtools_signals,
        sfa."InactivitySignals" AS inactivity_signals,
        sfa."SequenceGapWarnings" AS sequence_gap_warnings,
        sfa."RiskScore" AS risk_score,
        sfa."FeatureCountsJson" AS feature_counts_json,
        es."Status" AS status,
        es."StartedAtUtc" AS started_at_utc,
        es."SubmittedAtUtc" AS submitted_at_utc,
        es."SelfReportedCheated" AS self_reported_cheated,
        EXTRACT(EPOCH FROM (COALESCE(es."SubmittedAtUtc", es."LastActivityAtUtc") - es."StartedAtUtc")) AS duration_seconds
    FROM session_feature_aggregates sfa
    JOIN exam_sessions es ON es."Id" = sfa."SessionId"
    ORDER BY es."StartedAtUtc";
"""


def main():
    with get_connection() as conn:
        with conn.cursor(cursor_factory=psycopg2.extras.RealDictCursor) as cursor:
            cursor.execute(QUERY)
            rows = cursor.fetchall()

    if not rows:
        print("No sessions found - nothing to export.")
        return

    # Flatten feature_counts_json (per-event-type counts, kept generically
    # for every event type the collector ever records) into their own
    # columns, so new event types show up automatically without touching
    # this script.
    feature_keys = set()
    for row in rows:
        counts = parse_json_field(row["feature_counts_json"])
        feature_keys.update(counts.keys())
    feature_keys = sorted(feature_keys)

    fieldnames = [key for key in rows[0].keys() if key != "feature_counts_json"] + feature_keys

    OUTPUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    with OUTPUT_PATH.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            counts = parse_json_field(row.pop("feature_counts_json"))
            for key in feature_keys:
                row[key] = counts.get(key, 0)
            writer.writerow(row)

    print(f"Wrote {len(rows)} sessions to {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
