"""Raw session payload -> the exact feature tables the models were trained on.

Every transformation is delegated to ml/data (build_dataset.clean_events,
assemble_session_features, heartbeat_rows; build_question_dataset.question_rows),
so a live session goes through the same code as the training data.

Payload (JSON, camelCase - field names follow the database / C# entities):

{
  "sessionId": "uuid",
  "status": "submitted" | "abandoned" | "active",
  "startedAtUtc": "2026-11-03T12:31:05Z",
  "submittedAtUtc": "2026-11-03T12:40:11Z" | null,
  "lastActivityAtUtc": "2026-11-03T12:40:11Z",
  "questions": [ {"questionId": "uuid", "type": "single_choice|multiple_choice|free_text"} ],
  "answers":   [ {"questionId": "uuid"} ],                    # answered questions
  "events": [ {                                               # behavioral_events rows
      "id": "uuid", "seq": 12, "eventType": "window_blur",
      "timestampUtc": "...", "receivedAtUtc": "...",
      "questionId": "uuid" | null,
      "payload": { ... event-specific fields ... },
      "validationFlags": "" | "server_generated" | ...
  } ],
  "calibration": [ {                                          # optional, unused by the
      "fieldName": "participant_name", "characterCount": 10,  # active voters
      "totalDurationMs": 3072, "averageCharsPerSecond": 3.25,
      "correctionCount": 0, "keystrokeTimings": [ {"index":0,"dwellMs":68,"flightMs":null} ]
  } ]
}
"""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

import numpy as np
import pandas as pd

ML_DIR = Path(__file__).resolve().parents[1]
REPO_ROOT = ML_DIR.parent


def _load(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


bd = _load(ML_DIR / "data" / "build_dataset.py", "build_dataset")
bq = _load(ML_DIR / "data" / "build_question_dataset.py", "build_question_dataset")


def _ts(value) -> pd.Timestamp:
    """Same parser as the training data (build_dataset.to_utc)."""
    return bd.to_utc(pd.Series([value if value else None])).iloc[0]


def payload_to_tables(payload: dict, question_norms: dict) -> dict[str, pd.DataFrame]:
    """One session payload -> {"session": 1-row features, "heartbeats",
    "questions": per-question features}."""
    sid = payload["sessionId"]
    questions = payload.get("questions", [])
    started = _ts(payload.get("startedAtUtc"))
    submitted = _ts(payload.get("submittedAtUtc"))
    end = submitted if pd.notna(submitted) else _ts(payload.get("lastActivityAtUtc"))
    n_questions = len(questions)
    n_answered = len({a["questionId"] for a in payload.get("answers", [])})
    sessions = pd.DataFrame([{
        "session_id": sid,
        "status": payload.get("status", "submitted"),
        "is_abandoned": int(payload.get("status") == "abandoned"),
        "started_at_utc": started,
        "submitted_at_utc": submitted,
        "duration_sec": (end - started).total_seconds() if pd.notna(end) and pd.notna(started) else np.nan,
        "n_questions": n_questions,
        "n_free_text_questions": sum(q.get("type") == "free_text" for q in questions),
        "n_answered": n_answered,
        "answered_ratio": n_answered / n_questions if n_questions else np.nan,
    }])
    for col in ("started_at_utc", "submitted_at_utc"):   # same dtype as training (ns, UTC)
        sessions[col] = bd.to_utc(sessions[col].astype(object).where(sessions[col].notna(), None))

    events = pd.DataFrame([{
        "Id": e.get("id", i), "SessionId": sid, "Seq": e.get("seq", i),
        "TimestampUtc": e["timestampUtc"], "ReceivedAtUtc": e.get("receivedAtUtc") or e["timestampUtc"],
        "EventType": e["eventType"], "QuestionId": e.get("questionId"),
        "PayloadJson": json.dumps(e.get("payload") or {}), "ValidationFlags": e.get("validationFlags") or "",
    } for i, e in enumerate(payload.get("events", []))],
        columns=["Id", "SessionId", "Seq", "TimestampUtc", "ReceivedAtUtc", "EventType", "QuestionId",
                 "PayloadJson", "ValidationFlags"])
    client, server = bd.clean_events(events, sessions)

    calibration = pd.DataFrame([{
        "SessionId": sid, "FieldName": c.get("fieldName"), "CharacterCount": c.get("characterCount"),
        "TotalDurationMs": c.get("totalDurationMs"), "AverageCharsPerSecond": c.get("averageCharsPerSecond"),
        "CorrectionCount": c.get("correctionCount"),
        "KeystrokeTimingsJson": json.dumps(c.get("keystrokeTimings") or []),
    } for c in payload.get("calibration", [])],
        columns=["SessionId", "FieldName", "CharacterCount", "TotalDurationMs", "AverageCharsPerSecond",
                 "CorrectionCount", "KeystrokeTimingsJson"])
    cal = bd.calibration_rows(sessions, raw=calibration)

    session_row, _ = bd.assemble_session_features(sessions, client, server, cal)
    heartbeats = bd.heartbeat_rows(client)
    types = {q["questionId"]: q.get("type") for q in questions}
    question_rows = bq.question_rows(sessions, client, types, question_norms) if len(client) else pd.DataFrame()
    return {"session": session_row, "heartbeats": heartbeats, "questions": question_rows}


# ----------------------------------------------------------- demo / tests only

def payload_from_dump(session_id: str) -> dict:
    """Build the payload of a historical session from datasets/db_*.csv -
    exactly what the backend will send for a live one. Used by cli.py; not
    needed in production."""
    raw = _dump()
    s = raw["sessions"].set_index("Id").loc[session_id]
    qs = raw["questions"][raw["questions"]["AssessmentId"] == s["AssessmentId"]]
    ev = raw["events"][raw["events"]["SessionId"] == session_id]
    cal = raw["calibration"][raw["calibration"]["SessionId"] == session_id]
    ans = raw["answers"][raw["answers"]["SessionId"] == session_id]
    iso = lambda v: None if pd.isna(v) else str(v)
    return {
        "sessionId": session_id, "status": s["Status"],
        "startedAtUtc": iso(s["StartedAtUtc"]), "submittedAtUtc": iso(s["SubmittedAtUtc"]),
        "lastActivityAtUtc": iso(s["LastActivityAtUtc"]),
        "questions": [{"questionId": q, "type": t} for q, t in zip(qs["Id"], qs["Type"])],
        "answers": [{"questionId": q} for q in ans["QuestionId"]],
        "events": [{
            "id": r.Id, "seq": int(r.Seq), "eventType": r.EventType, "timestampUtc": r.TimestampUtc,
            "receivedAtUtc": r.ReceivedAtUtc, "questionId": None if pd.isna(r.QuestionId) else r.QuestionId,
            "payload": json.loads(r.PayloadJson) if isinstance(r.PayloadJson, str) else {},
            "validationFlags": "" if pd.isna(r.ValidationFlags) else r.ValidationFlags,
        } for r in ev.itertuples()],
        "calibration": [{
            "fieldName": r.FieldName, "characterCount": r.CharacterCount, "totalDurationMs": r.TotalDurationMs,
            "averageCharsPerSecond": r.AverageCharsPerSecond, "correctionCount": r.CorrectionCount,
            "keystrokeTimings": json.loads(r.KeystrokeTimingsJson) if isinstance(r.KeystrokeTimingsJson, str) else [],
        } for r in cal.itertuples()],
    }


_dump_cache: dict = {}


def _dump() -> dict:
    if not _dump_cache:
        d = REPO_ROOT / "datasets"
        _dump_cache.update({
            "sessions": pd.read_csv(d / "db_exam_sessions.csv"),
            "questions": pd.read_csv(d / "db_questions.csv"),
            "events": pd.read_csv(d / "db_behavioral_events.csv", low_memory=False),
            "calibration": pd.read_csv(d / "db_calibration_samples.csv"),
            "answers": pd.read_csv(d / "db_student_answers.csv"),
        })
    return _dump_cache
