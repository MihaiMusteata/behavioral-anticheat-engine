"""Build the common, filtered ML dataset from the raw production dump.

Input:  datasets/db_*.csv  (full table dump of the production database)
Output: datasets/ml/       (gitignored, like everything else in datasets/)

    sessions.csv          one row per kept session: label, group, context,
                          behavioral counts/rates, timing, typing, calibration
    heartbeats.csv        one row per state_heartbeat (~8s) + leak-free running
                          counts of behavioral events as-of that moment
    typing_samples.csv    one row per typing_speed_sample (free_text answers)
    calibration.csv       one row per calibration field (name / specialty)
    events_clean.csv      the cleaned client event log (all kept events)
    feature_catalog.json  which sessions.csv column plays which role
                          (meta / label / feature group / excluded-for-leakage)
    filter_report.json    how many sessions/events each filtering rule removed

Every model under ml/models/ reads these files and picks the columns it
needs via feature_catalog.json - nothing model-specific happens here.

Run from the repo root:  ml/.venv/bin/python ml/data/build_dataset.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd

REPO_ROOT = Path(__file__).resolve().parents[2]
RAW_DIR = REPO_ROOT / "datasets"
OUT_DIR = REPO_ROOT / "datasets" / "ml"

# A typing sample above this speed is not physically typed text (copy-paste /
# autofill burst). Same threshold already used for the WPM distribution study.
BURST_CPS_THRESHOLD = 15.0

# Events written by the server about the stream itself, not by the student.
SERVER_GENERATED = "server_generated"

# Client event types used for counts/rates. Fixed list so every type gets a
# column (zero-filled) even if it never occurred - e.g. print_attempt.
BEHAVIOR_EVENT_TYPES = [
    "window_blur", "window_focus", "visibility_change",
    "copy", "cut", "paste", "text_burst_detected", "text_selected",
    "keyboard_shortcut", "right_click", "print_attempt",
    "devtools_heuristic_triggered", "window_resize", "fullscreen_exit",
    "idle_detected", "multiple_tabs_detected",
    "page_reload", "page_unload", "session_resume",
    "answer_change", "answer_saved",
    "question_navigation", "question_view_start", "question_view_end",
    "typing_speed_sample", "state_heartbeat",
]

# Subset carried as running ("so far") counts on every heartbeat row.
RUNNING_COUNT_TYPES = [
    "window_blur", "visibility_change", "copy", "cut", "paste",
    "text_burst_detected", "text_selected", "keyboard_shortcut", "right_click",
    "devtools_heuristic_triggered", "window_resize", "idle_detected",
    "multiple_tabs_detected", "page_reload", "answer_change", "answer_saved",
    "question_navigation",
]

SELECTION_ZONES = ["question_option", "question_prompt"]   # text_selected payload "zone"

SHORTCUTS = {
    "Ctrl+C": "ctrl_c", "Ctrl+V": "ctrl_v", "Ctrl+X": "ctrl_x", "Ctrl+A": "ctrl_a",
    "Ctrl+P": "ctrl_p", "Ctrl+F": "ctrl_f", "F12": "f12",
    "Ctrl+Shift+I": "ctrl_shift_i", "Ctrl+Shift+J": "ctrl_shift_j",
    "Ctrl+Shift+C": "ctrl_shift_c", "PrintScreen": "printscreen",
}


# --------------------------------------------------------------------------- io

def read_raw(table: str) -> pd.DataFrame:
    path = RAW_DIR / f"db_{table}.csv"
    if not path.exists():
        sys.exit(f"missing {path} - export the production database first")
    return pd.read_csv(path, low_memory=False)


def to_utc(series: pd.Series) -> pd.Series:
    return pd.to_datetime(series, utc=True, format="ISO8601")


def parse_payload(raw: str) -> dict:
    try:
        value = json.loads(raw) if isinstance(raw, str) else {}
    except json.JSONDecodeError:
        return {}
    return value if isinstance(value, dict) else {}


# ------------------------------------------------------------------- sessions

def label_from_group_name(name: str) -> float:
    lowered = str(name).lower()
    if "cheating" in lowered:
        return 1.0
    if "natural" in lowered:
        return 0.0
    return np.nan


def test_from_title(title: str) -> tuple[str, str]:
    """('Test PC, prof X (greu)') -> ('PC', 'greu'); variant is '' when none."""
    title = str(title)
    test = title.replace("Test ", "", 1).split(",")[0].strip()
    variant = ""
    if "(greu)" in title:
        variant = "greu"
    elif "(ușor)" in title or "(usor)" in title:
        variant = "usor"
    elif title.rstrip()[-2:] in (" 1", " 2"):
        variant = title.rstrip()[-1]
    return test, variant


def load_sessions(report: dict) -> pd.DataFrame:
    sessions = read_raw("exam_sessions")
    schedules = read_raw("exam_schedules")
    assessments = read_raw("assessments")
    questions = read_raw("questions")
    answers = read_raw("student_answers")

    report["sessions_raw"] = len(sessions)

    sched = schedules[["Id", "Name", "AssessmentId"]].rename(
        columns={"Id": "ExamScheduleId", "Name": "group_name", "AssessmentId": "sched_assessment"})
    df = sessions.merge(sched, on="ExamScheduleId", how="left")
    df = df.merge(assessments[["Id", "Title"]].rename(columns={"Id": "AssessmentId", "Title": "assessment_title"}),
                  on="AssessmentId", how="left")

    df["label"] = df["group_name"].map(label_from_group_name)
    tv = df["assessment_title"].map(test_from_title)
    df["test"] = tv.map(lambda x: x[0])
    df["test_variant"] = tv.map(lambda x: x[1])

    # --- session filters, applied in order, each one counted
    filters = []

    mask = df["label"].isna()
    filters.append({"rule": "schedule_without_natural_cheating_label",
                    "removed": int(mask.sum()),
                    "detail": sorted(df.loc[mask, "group_name"].astype(str).unique().tolist())})
    df = df[~mask]

    mask = df["Status"].eq("active")
    filters.append({"rule": "session_still_active_unfinished", "removed": int(mask.sum())})
    df = df[~mask]

    answered = answers.groupby("SessionId").size()
    df["n_answered"] = df["Id"].map(answered).fillna(0).astype(int)
    mask = df["n_answered"].eq(0)
    filters.append({"rule": "session_with_zero_answers", "removed": int(mask.sum())})
    df = df[~mask]

    report["session_filters"] = filters
    report["sessions_kept"] = len(df)

    # --- per-assessment question structure
    qcount = questions.groupby("AssessmentId").size()
    qfree = questions[questions["Type"].eq("free_text")].groupby("AssessmentId").size()
    df["n_questions"] = df["AssessmentId"].map(qcount).fillna(0).astype(int)
    df["n_free_text_questions"] = df["AssessmentId"].map(qfree).fillna(0).astype(int)
    df["answered_ratio"] = df["n_answered"] / df["n_questions"].replace(0, np.nan)

    # --- auto-scored outcome (choice questions only; free_text is manual)
    auto = answers[answers["RequiresManualScoring"].astype(str).eq("f")]
    auto = auto.merge(questions[["Id", "Points"]].rename(columns={"Id": "QuestionId"}), on="QuestionId", how="left")
    agg = auto.groupby("SessionId").agg(auto_score_sum=("AutoScore", "sum"), auto_points_sum=("Points", "sum"))
    df = df.merge(agg, left_on="Id", right_index=True, how="left")
    df["auto_score_ratio"] = df["auto_score_sum"] / df["auto_points_sum"].replace(0, np.nan)

    # --- timing
    for col in ("StartedAtUtc", "SubmittedAtUtc", "LastActivityAtUtc"):
        df[col] = to_utc(df[col])
    end = df["SubmittedAtUtc"].fillna(df["LastActivityAtUtc"])
    df["duration_sec"] = (end - df["StartedAtUtc"]).dt.total_seconds()
    df["is_abandoned"] = df["Status"].eq("abandoned").astype(int)

    df["self_reported_cheated"] = df["SelfReportedCheated"].map({"t": 1, "f": 0})

    keep = {
        "Id": "session_id", "ExamScheduleId": "schedule_id", "group_name": "group_name",
        "assessment_title": "assessment_title", "test": "test", "test_variant": "test_variant",
        "label": "label", "Status": "status", "is_abandoned": "is_abandoned",
        "StartedAtUtc": "started_at_utc", "SubmittedAtUtc": "submitted_at_utc",
        "duration_sec": "duration_sec", "n_questions": "n_questions",
        "n_free_text_questions": "n_free_text_questions", "n_answered": "n_answered",
        "answered_ratio": "answered_ratio", "auto_score_ratio": "auto_score_ratio",
        "self_reported_cheated": "self_reported_cheated", "CurrentRiskScore": "rule_risk_score",
        "SequenceGapWarnings": "sequence_gap_warnings",
    }
    df = df[list(keep)].rename(columns=keep)
    df["label"] = df["label"].astype(int)

    # Stable, human-readable group ids for leave-one-group-out CV.
    groups = df.groupby("group_name")["started_at_utc"].min().sort_values().index
    group_ids = {name: f"G{idx + 1:02d}" for idx, name in enumerate(groups)}
    df["group_id"] = df["group_name"].map(group_ids)
    return df.reset_index(drop=True)


# --------------------------------------------------------------------- events

def load_events(sessions: pd.DataFrame, report: dict) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Return (client_events, server_events) for kept sessions, cleaned."""
    ev = read_raw("behavioral_events")
    report["events_raw"] = len(ev)
    return clean_events(ev, sessions, report)


def clean_events(ev: pd.DataFrame, sessions: pd.DataFrame, report: dict | None = None) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Raw behavioral_events rows (DB column names) -> (client, server), cleaned.
    Shared with ml/inference, so a live session is cleaned exactly like the
    training data. `sessions` needs session_id + submitted_at_utc."""
    report = {} if report is None else report
    ev = ev[ev["SessionId"].isin(sessions["session_id"])].copy()
    report["events_in_kept_sessions"] = len(ev)

    ev["ts"] = to_utc(ev["TimestampUtc"])
    ev["received"] = to_utc(ev["ReceivedAtUtc"])
    ev["payload"] = ev["PayloadJson"].map(parse_payload)

    is_server = ev["ValidationFlags"].astype(str).str.contains(SERVER_GENERATED, na=False)
    server = ev[is_server].copy()
    client = ev[~is_server].copy()
    report["server_generated_events_separated"] = {
        "total": len(server), "by_type": server["EventType"].value_counts().to_dict()}

    # Cutoff: drop client events after the student submitted. Client clocks
    # can be off by minutes, so compare in client time: the client's own
    # session_end(reason=submit) when present, otherwise the server submit time
    # shifted by the session's median client->server clock offset.
    sess = sessions.set_index("session_id")
    submit_end = (client[(client["EventType"] == "session_end")
                         & client["payload"].map(lambda p: p.get("reason") == "submit")]
                  .groupby("SessionId")["ts"].max())
    offset = (client["received"] - client["ts"]).groupby(client["SessionId"]).median()
    server_submit = sess["submitted_at_utc"]
    cutoff = submit_end.reindex(sess.index)
    fallback = server_submit - offset.reindex(sess.index)
    cutoff = cutoff.fillna(fallback) + pd.Timedelta(seconds=2)

    client["cutoff"] = client["SessionId"].map(cutoff)
    after = client["cutoff"].notna() & (client["ts"] > client["cutoff"])
    report["client_events_after_submit_removed"] = {
        "total": int(after.sum()), "by_type": client.loc[after, "EventType"].value_counts().to_dict()}
    client = client[~after]

    # t_sec: seconds since the session's first client event (client clock,
    # internally consistent within a session).
    t0 = client.groupby("SessionId")["ts"].transform("min")
    client["t_sec"] = (client["ts"] - t0).dt.total_seconds()
    client = client.sort_values(["SessionId", "ts", "Seq"], kind="stable").reset_index(drop=True)
    report["client_events_kept"] = len(client)
    return client, server


# ------------------------------------------------------------ session features

def payload_num(frame: pd.DataFrame, key: str) -> pd.Series:
    return pd.to_numeric(frame["payload"].map(lambda p: p.get(key)), errors="coerce")


def session_event_features(sessions: pd.DataFrame, client: pd.DataFrame, server: pd.DataFrame) -> pd.DataFrame:
    sid = sessions["session_id"]
    out = pd.DataFrame(index=sid)
    by = client.groupby("SessionId")

    counts = client.pivot_table(index="SessionId", columns="EventType", values="Id",
                                aggfunc="count", fill_value=0)
    for t in BEHAVIOR_EVENT_TYPES:
        out[f"{t}_count"] = counts[t].reindex(sid).fillna(0).astype(int) if t in counts else 0

    # keyboard shortcuts broken down
    ks = client[client["EventType"] == "keyboard_shortcut"].copy()
    ks["shortcut"] = ks["payload"].map(lambda p: SHORTCUTS.get(p.get("shortcut"), "other"))
    kc = ks.pivot_table(index="SessionId", columns="shortcut", values="Id", aggfunc="count", fill_value=0)
    for name in sorted(set(SHORTCUTS.values()) | {"other"}):
        out[f"shortcut_{name}_count"] = kc[name].reindex(sid).fillna(0).astype(int) if name in kc else 0

    # blur / away-from-tab time
    focus = client[client["EventType"] == "window_focus"].copy()
    focus["blur_ms"] = payload_num(focus, "blurDurationMs")
    g = focus.groupby("SessionId")["blur_ms"]
    out["blur_total_sec"] = (g.sum() / 1000).reindex(sid).fillna(0)
    out["blur_max_sec"] = (g.max() / 1000).reindex(sid).fillna(0)
    out["blur_mean_sec"] = (g.mean() / 1000).reindex(sid).fillna(0)

    # clipboard volume
    for t in ("copy", "cut", "paste"):
        sub = client[client["EventType"] == t]
        out[f"{t}_chars_total"] = payload_num(sub, "textLength").groupby(sub["SessionId"]).sum().reindex(sid).fillna(0)
    bursts = client[client["EventType"] == "text_burst_detected"]
    out["text_burst_chars_total"] = payload_num(bursts, "charDelta").groupby(bursts["SessionId"]).sum().reindex(sid).fillna(0)

    # text selection by zone
    sel = client[client["EventType"] == "text_selected"].copy()
    sel["zone"] = sel["payload"].map(lambda p: p.get("zone") or "unknown")
    zc = sel.pivot_table(index="SessionId", columns="zone", values="Id", aggfunc="count", fill_value=0)
    # Fixed zone list (plus any new one seen), so a session without selections
    # still gets the columns - with 0, as in training.
    for zone in sorted(set(SELECTION_ZONES) | set(zc.columns)):
        out[f"text_selected_{zone}_count"] = zc[zone].reindex(sid).fillna(0).astype(int) if zone in zc else 0
    out["text_selected_chars_total"] = payload_num(sel, "selectionLength").groupby(sel["SessionId"]).sum().reindex(sid).fillna(0)

    # idle
    idle = client[client["EventType"] == "idle_detected"]
    out["idle_total_sec"] = (payload_num(idle, "idleMs").groupby(idle["SessionId"]).sum() / 1000).reindex(sid).fillna(0)

    # per-question view time
    view = client[client["EventType"] == "question_view_end"].copy()
    view["dur"] = payload_num(view, "durationMs") / 1000
    vg = view.groupby("SessionId")["dur"]
    out["question_view_mean_sec"] = vg.mean().reindex(sid)
    out["question_view_median_sec"] = vg.median().reindex(sid)
    out["question_view_std_sec"] = vg.std().reindex(sid)
    out["question_view_min_sec"] = vg.min().reindex(sid)
    out["question_view_max_sec"] = vg.max().reindex(sid)
    out["question_view_cv"] = out["question_view_std_sec"] / out["question_view_mean_sec"].replace(0, np.nan)
    out["question_view_under_5s_ratio"] = view.assign(f=view["dur"] < 5).groupby("SessionId")["f"].mean().reindex(sid)

    # navigation
    nav = client[client["EventType"] == "question_navigation"].copy()
    nav["dir"] = nav["payload"].map(lambda p: p.get("direction"))
    out["navigation_backward_count"] = nav[nav["dir"] == "backward"].groupby("SessionId").size().reindex(sid).fillna(0).astype(int)
    vs = client[client["EventType"] == "question_view_start"].copy()
    vs["q"] = vs["payload"].map(lambda p: p.get("questionId"))
    views_per_q = vs.groupby(["SessionId", "q"]).size()
    out["question_revisit_count"] = (views_per_q - 1).clip(lower=0).groupby(level=0).sum().reindex(sid).fillna(0).astype(int)

    # heartbeat state aggregates
    hb = client[client["EventType"] == "state_heartbeat"].copy()
    for key in ("idleMs", "mouseX", "mouseY", "scrollY", "windowGapWidth", "windowGapHeight"):
        hb[key] = payload_num(hb, key)
    hg = hb.groupby("SessionId")
    out["hb_mouse_missing_ratio"] = hg["mouseX"].apply(lambda s: s.isna().mean()).reindex(sid)
    out["hb_idle_mean_sec"] = (hg["idleMs"].mean() / 1000).reindex(sid)
    out["hb_idle_max_sec"] = (hg["idleMs"].max() / 1000).reindex(sid)
    out["hb_scroll_std"] = hg["scrollY"].std().reindex(sid)
    out["hb_window_gap_w_max"] = hg["windowGapWidth"].max().reindex(sid)
    out["hb_window_gap_h_max"] = hg["windowGapHeight"].max().reindex(sid)
    out["hb_window_gap_w_mean"] = hg["windowGapWidth"].mean().reindex(sid)
    out["hb_window_gap_h_mean"] = hg["windowGapHeight"].mean().reindex(sid)

    # device context (mobile vs desktop is a confounder, not a cheating signal)
    starts = client[client["EventType"].isin(["session_start", "session_resume"])].copy()
    starts["ua"] = starts["payload"].map(lambda p: str(p.get("userAgent", "")))
    mobile = starts.groupby("SessionId")["ua"].apply(lambda s: int(s.str.contains("Mobile|Android|iPhone", regex=True).any()))
    out["is_mobile"] = mobile.reindex(sid).fillna(0).astype(int)

    # stream-quality metadata (server-generated, kept apart from behavior)
    sc = server.pivot_table(index="SessionId", columns="EventType", values="Id", aggfunc="count", fill_value=0)
    for t in ("event_validation_failed", "event_silence_detected", "event_sequence_gap", "session_abandoned"):
        out[f"server_{t}_count"] = sc[t].reindex(sid).fillna(0).astype(int) if t in sc else 0

    out.index.name = "session_id"
    return out.reset_index()


# --------------------------------------------------------------------- typing

def typing_samples(client: pd.DataFrame) -> pd.DataFrame:
    ts = client[client["EventType"] == "typing_speed_sample"].copy()
    cols = {"sampleMs": "sample_ms", "activeMs": "active_ms", "pauseMs": "pause_ms",
            "pauseCount": "pause_count", "answerLength": "answer_length",
            "charsPerSecond": "cps", "activeCharsPerSecond": "active_cps", "questionIndex": "question_index"}
    for src, dst in cols.items():
        ts[dst] = payload_num(ts, src)
    ts["question_id"] = ts["payload"].map(lambda p: p.get("questionId"))
    ts["chars_typed"] = ts["cps"] * ts["sample_ms"] / 1000
    ts["is_burst"] = (ts["active_cps"] > BURST_CPS_THRESHOLD).astype(int)
    return ts[["SessionId", "t_sec", "question_id", *cols.values(), "chars_typed", "is_burst"]] \
        .rename(columns={"SessionId": "session_id"}).reset_index(drop=True)


def session_typing_features(sessions: pd.DataFrame, ts: pd.DataFrame) -> pd.DataFrame:
    sid = sessions["session_id"]
    clean = ts[(ts["is_burst"] == 0) & (ts["active_ms"] > 0)]
    g_all = ts.groupby("session_id")
    g = clean.groupby("session_id")
    out = pd.DataFrame(index=sid)
    out["typing_burst_samples_count"] = g_all["is_burst"].sum().reindex(sid).fillna(0).astype(int)
    out["typing_chars_total"] = g_all["chars_typed"].sum().reindex(sid).fillna(0)
    # active speed weighted by active time: total chars / total active seconds
    out["typing_active_cps"] = (g["chars_typed"].sum() / (g["active_ms"].sum() / 1000)).reindex(sid)
    out["typing_total_cps"] = (g["chars_typed"].sum() / (g["sample_ms"].sum() / 1000)).reindex(sid)
    out["typing_active_cps_median"] = g["active_cps"].median().reindex(sid)
    out["typing_active_cps_p90"] = g["active_cps"].quantile(0.9).reindex(sid)
    out["typing_pause_ratio"] = (g_all["pause_ms"].sum() / g_all["sample_ms"].sum().replace(0, np.nan)).reindex(sid)
    out["typing_pause_count"] = g_all["pause_count"].sum().reindex(sid).fillna(0)
    out.index.name = "session_id"
    return out.reset_index()


# ---------------------------------------------------------------- calibration

def calibration_rows(sessions: pd.DataFrame, raw: pd.DataFrame | None = None) -> pd.DataFrame:
    """calibration_samples rows (DB column names) -> per-field stats. `raw`
    defaults to the dump; ml/inference passes a live session's samples."""
    cal = read_raw("calibration_samples") if raw is None else raw
    cal = cal[cal["SessionId"].isin(sessions["session_id"])].copy()

    def timing_stats(raw: str) -> dict:
        try:
            keys = json.loads(raw) if isinstance(raw, str) else []
        except json.JSONDecodeError:
            keys = []
        dwell = np.array([k["dwellMs"] for k in keys if k.get("dwellMs") is not None], dtype=float)
        flight = np.array([k["flightMs"] for k in keys if k.get("flightMs") is not None], dtype=float)
        stat = lambda a, f: float(f(a)) if a.size else np.nan
        return {
            "dwell_mean_ms": stat(dwell, np.mean), "dwell_std_ms": stat(dwell, np.std),
            "flight_median_ms": stat(flight, np.median),
            "flight_iqr_ms": stat(flight, lambda a: np.percentile(a, 75) - np.percentile(a, 25)),
            "keystrokes": int(len(keys)),
        }

    stats = pd.DataFrame(cal["KeystrokeTimingsJson"].map(timing_stats).tolist(), index=cal.index,
                         columns=["dwell_mean_ms", "dwell_std_ms", "flight_median_ms", "flight_iqr_ms", "keystrokes"])
    cal = pd.concat([cal, stats], axis=1)
    cal = cal.rename(columns={"SessionId": "session_id", "FieldName": "field", "CharacterCount": "chars",
                              "TotalDurationMs": "duration_ms", "AverageCharsPerSecond": "cps",
                              "CorrectionCount": "corrections"})
    return cal[["session_id", "field", "chars", "duration_ms", "cps", "corrections",
                "dwell_mean_ms", "dwell_std_ms", "flight_median_ms", "flight_iqr_ms", "keystrokes"]] \
        .reset_index(drop=True)


def session_calibration_features(sessions: pd.DataFrame, cal: pd.DataFrame) -> pd.DataFrame:
    sid = sessions["session_id"]
    ok = cal[cal["cps"] <= BURST_CPS_THRESHOLD]  # autofill would not be a baseline
    g = ok.groupby("session_id")
    out = pd.DataFrame(index=sid)
    out["has_calibration"] = sid.isin(ok["session_id"]).astype(int).values
    out["calib_cps"] = (g["chars"].sum() / (g["duration_ms"].sum() / 1000)).reindex(sid)
    out["calib_corrections"] = g["corrections"].sum().reindex(sid)
    out["calib_dwell_mean_ms"] = g["dwell_mean_ms"].mean().reindex(sid)
    out["calib_flight_median_ms"] = g["flight_median_ms"].mean().reindex(sid)
    out.index.name = "session_id"
    return out.reset_index()


# ----------------------------------------------------------------- heartbeats

def heartbeat_rows(client: pd.DataFrame) -> pd.DataFrame:
    """Heartbeats + running counts computed on the time-sorted event stream, so
    each row only sees events that happened before it (no future leakage)."""
    onehot = pd.DataFrame({t: (client["EventType"] == t).astype(int) for t in RUNNING_COUNT_TYPES})
    running = onehot.groupby(client["SessionId"]).cumsum()
    running.columns = [f"{t}_so_far" for t in RUNNING_COUNT_TYPES]

    is_hb = client["EventType"] == "state_heartbeat"
    hb = client.loc[is_hb, ["SessionId", "t_sec", "payload"]].copy()
    for key, col in (("idleMs", "idle_ms"), ("mouseX", "mouse_x"), ("mouseY", "mouse_y"),
                     ("scrollY", "scroll_y"), ("windowGapWidth", "window_gap_w"),
                     ("windowGapHeight", "window_gap_h"), ("questionIndex", "question_index")):
        hb[col] = payload_num(hb, key)
    hb["mouse_missing"] = hb["mouse_x"].isna().astype(int)
    hb["hb_index"] = hb.groupby("SessionId").cumcount()
    hb = pd.concat([hb.drop(columns="payload"), running.loc[is_hb]], axis=1)
    return hb.rename(columns={"SessionId": "session_id"}).reset_index(drop=True)


# ------------------------------------------------------------------- catalog

def build_catalog(df: pd.DataFrame) -> dict:
    columns = df.columns.tolist()
    meta = ["session_id", "schedule_id", "group_name", "group_id", "assessment_title",
            "test", "test_variant", "status", "started_at_utc", "submitted_at_utc"]
    excluded = {
        "self_reported_cheated": "near-label (student's own answer to 'did you cheat?')",
        "rule_risk_score": "output of the current rule-based scorer - comparison baseline, not an input",
        "sequence_gap_warnings": "stream-quality artefact, not behavior",
    }
    server_quality = [c for c in columns if c.startswith("server_")]
    reserved = set(meta) | {"label"} | set(excluded) | set(server_quality)
    candidates = [c for c in columns if c not in reserved]
    # Never-observed signals (e.g. PrintScreen) carry no information.
    constant = [c for c in candidates if df[c].nunique(dropna=True) <= 1]

    # First matching rule wins, so every feature lands in exactly one group.
    rules = [
        ("behavior_rates", lambda c: c.endswith("_per_min") or c.endswith("_per_question")),
        ("calibration", lambda c: c.startswith("calib_") or c.startswith("typing_vs_calib") or c == "has_calibration"),
        ("typing", lambda c: c.startswith("typing_")),
        ("heartbeat_summary", lambda c: c.startswith("hb_")),
        ("timing", lambda c: c.startswith("question_view_") or c in (
            "duration_sec", "n_answered", "answered_ratio", "navigation_backward_count", "question_revisit_count")),
        ("context", lambda c: c in ("is_mobile", "n_questions", "n_free_text_questions", "is_abandoned")),
        ("outcome", lambda c: c == "auto_score_ratio"),
        ("behavior_counts", lambda c: True),
    ]
    groups: dict[str, list[str]] = {name: [] for name, _ in rules}
    for col in candidates:
        if col in constant:
            continue
        name = next(name for name, match in rules if match(col))
        groups[name].append(col)

    return {
        "label": "label",
        "label_meaning": "1 = group instructed to cheat, 0 = group instructed to behave naturally",
        "cv_group_column": "group_id",
        "meta": meta,
        "excluded_leakage": excluded,
        "server_quality": server_quality,
        "constant_dropped": constant,
        "feature_groups": groups,
        "notes": {
            "context": "confounders (device, test structure) - useful for stratified analysis, use as model input with care",
            "outcome": "auto-score depends on test difficulty; PC 'greu' was given only to cheating groups",
            "devtools_heuristic_triggered_count": "noisy heuristic - also fires in ~30% of natural sessions",
        },
    }


# ------------------------------------------------------------------ assembly

def assemble_session_features(sessions: pd.DataFrame, client: pd.DataFrame, server: pd.DataFrame,
                              cal: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
    """Cleaned events -> one feature row per session (sessions.csv layout) +
    the typing-sample table. Single source of truth for training AND for
    ml/inference: any change here changes both, never only one."""
    ev_feats = session_event_features(sessions, client, server)
    ts = typing_samples(client)
    ty_feats = session_typing_features(sessions, ts)
    cal_feats = session_calibration_features(sessions, cal)

    df = sessions.merge(ev_feats, on="session_id").merge(ty_feats, on="session_id").merge(cal_feats, on="session_id")

    # Exam vs. own calibration (the per-student baseline), only where both exist.
    df["typing_vs_calib_active_ratio"] = df["typing_active_cps"] / df["calib_cps"]
    df["typing_vs_calib_active_diff"] = df["typing_active_cps"] - df["calib_cps"]

    # Rates: tests differ in length (10-15 questions) and duration, raw totals
    # are not comparable across tests.
    minutes = (df["duration_sec"] / 60).replace(0, np.nan)
    rate_sources = [f"{t}_count" for t in BEHAVIOR_EVENT_TYPES
                    if t not in ("state_heartbeat", "question_view_start", "question_view_end")] \
        + ["blur_total_sec", "paste_chars_total", "copy_chars_total", "idle_total_sec"]
    for col in rate_sources:
        df[f"{col}_per_min"] = df[col] / minutes
        df[f"{col}_per_question"] = df[col] / df["n_questions"].replace(0, np.nan)
    df["blur_time_ratio"] = df["blur_total_sec"] / df["duration_sec"].replace(0, np.nan)
    return df, ts


# ---------------------------------------------------------------------- main

def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    report: dict = {"source": str(RAW_DIR.relative_to(REPO_ROOT)), "burst_cps_threshold": BURST_CPS_THRESHOLD}

    sessions = load_sessions(report)
    client, server = load_events(sessions, report)
    cal = calibration_rows(sessions)
    df, ts = assemble_session_features(sessions, client, server, cal)

    # Attach label/group to the long tables so each model can load one file.
    lg = df[["session_id", "label", "group_id", "test"]]
    hb = heartbeat_rows(client).merge(lg, on="session_id")
    ts = ts.merge(lg, on="session_id")
    cal = cal.merge(lg, on="session_id")
    events = client[["SessionId", "t_sec", "Seq", "EventType", "QuestionId", "PayloadJson"]] \
        .rename(columns={"SessionId": "session_id", "Seq": "seq", "EventType": "event_type",
                         "QuestionId": "question_id", "PayloadJson": "payload_json"}).merge(lg, on="session_id")

    df.to_csv(OUT_DIR / "sessions.csv", index=False)
    hb.to_csv(OUT_DIR / "heartbeats.csv", index=False)
    ts.to_csv(OUT_DIR / "typing_samples.csv", index=False)
    cal.to_csv(OUT_DIR / "calibration.csv", index=False)
    events.to_csv(OUT_DIR / "events_clean.csv", index=False)

    catalog = build_catalog(df)
    (OUT_DIR / "feature_catalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=False))

    report["final"] = {
        "sessions": len(df),
        "label_counts": {"natural": int((df["label"] == 0).sum()), "cheating": int((df["label"] == 1).sum())},
        "groups": int(df["group_id"].nunique()),
        "sessions_by_group": df.groupby(["group_id", "group_name", "label"]).size()
            .reset_index(name="n").to_dict(orient="records"),
        "sessions_by_test": df.groupby(["test", "label"]).size().unstack(fill_value=0)
            .rename(columns={0: "natural", 1: "cheating"}).to_dict(orient="index"),
        "sessions_with_calibration": int(df["has_calibration"].sum()),
        "sessions_with_typing": int((df["typing_speed_sample_count"] > 0).sum()),
        "sessions_with_both": int(((df["has_calibration"] == 1) & df["typing_active_cps"].notna()).sum()),
        "heartbeat_rows": len(hb),
        "typing_sample_rows": len(ts),
        "calibration_rows": len(cal),
        "clean_event_rows": len(events),
        "session_feature_columns": sum(len(v) for v in catalog["feature_groups"].values()),
    }
    (OUT_DIR / "filter_report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False, default=str))

    f = report["final"]
    print(f"sessions: {report['sessions_raw']} raw -> {f['sessions']} kept "
          f"({f['label_counts']['natural']} natural / {f['label_counts']['cheating']} cheating, {f['groups']} groups)")
    for rule in report["session_filters"]:
        print(f"  - {rule['rule']}: {rule['removed']}")
    print(f"events: {report['events_raw']} raw -> {report['client_events_kept']} clean client events "
          f"({report['server_generated_events_separated']['total']} server-generated separated, "
          f"{report['client_events_after_submit_removed']['total']} after submit removed)")
    print(f"calibration: {f['sessions_with_calibration']} sessions, typing: {f['sessions_with_typing']}, both: {f['sessions_with_both']}")
    print(f"rows: heartbeats {f['heartbeat_rows']}, typing {f['typing_sample_rows']}, "
          f"calibration {f['calibration_rows']}, events {f['clean_event_rows']}")
    print(f"session feature columns: {f['session_feature_columns']}  ->  {OUT_DIR.relative_to(REPO_ROOT)}/")


if __name__ == "__main__":
    main()
