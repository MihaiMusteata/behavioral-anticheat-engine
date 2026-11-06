"""Build the question-level dataset: one row per (session, question).

Input:  datasets/db_*.csv (same dump as build_dataset.py, same session filters)
Output: datasets/ml/
    questions.csv                one row per question a student opened
    question_feature_catalog.json  feature list + roles
    question_norms.json          per-question peer medians (see below)

Why a separate dataset: the session voters learned totals over a whole exam
(~7 min). Fed a single question (~20 s) they mis-read it - short duration
looks like a "rushed session" (per-question experiment, 2026-11-09). Models
that score single questions must be trained on question-sized rows.

Labels are WEAK: every question inherits its session's group label. A student
of a cheating group did not necessarily cheat on every question, so cheating
rows contain honest questions (multiple-instance learning setting). Natural
rows, on the other hand, are reliable "honest question" examples.

Peer-relative features: time on a question only means something compared to
other students on the SAME question (60 s is normal on a hard one, odd on an
easy one). `*_rel` = (value + 1) / (median over all students on that question
+ 1). The medians use no labels (all students who answered the question) and
are stored in question_norms.json so ml/inference applies the same values;
for a question never seen in training, the median of its question type is
used.

Run from the repo root:  ml/.venv/bin/python ml/data/build_question_dataset.py
"""

from __future__ import annotations

import importlib.util
import json
from pathlib import Path

import numpy as np
import pandas as pd

HERE = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("build_dataset", HERE / "build_dataset.py")
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

OUT_DIR = bd.OUT_DIR

# Same weights as appsettings.json -> RiskScoring:Rules, applied to one
# question (baseline only, never a model input).
RULE_WEIGHTS = {   # question column -> weight
    "blur_count": 3, "visibility_hidden_count": 5, "copy_count": 4, "cut_count": 4, "paste_count": 7,
    "window_resize_count": 3, "right_click_count": 2, "shortcut_copy_paste_count": 6, "shortcut_other_count": 6,
    "text_burst_count": 12, "devtools_count": 18, "print_attempt_count": 10, "idle_count": 2,
    "multiple_tabs_count": 14,
}

COPY_PASTE_SHORTCUTS = {"Ctrl+C", "Ctrl+V", "Ctrl+X"}

QUESTION_FEATURES = [
    # time on the question
    "view_sec", "view_sec_rel", "view_count", "revisits",
    # leaving the exam tab
    "blur_count", "blur_total_sec", "blur_max_sec", "blur_time_ratio", "visibility_hidden_count",
    # clipboard / text appearing at once
    "copy_count", "copy_chars", "cut_count", "paste_count", "paste_chars",
    "text_burst_count", "text_burst_chars",
    # selecting text
    "prompt_selected_count", "prompt_selected_chars", "option_selected_count",
    # keyboard / browser
    "shortcut_copy_paste_count", "shortcut_other_count", "right_click_count", "devtools_count",
    "window_resize_count", "multiple_tabs_count", "idle_count", "idle_total_sec",
    # answering
    "answer_change_count", "answer_change_rel", "answer_saved_count",
    # typing (free_text only, NaN elsewhere)
    "typing_samples_count", "typing_chars", "typing_active_cps", "typing_max_active_cps",
    "typing_burst_samples", "typing_pause_ratio",
    # question type (context)
    "is_free_text", "is_multiple_choice",
]


def _num(frame: pd.DataFrame, key: str) -> pd.Series:
    return pd.to_numeric(frame["payload"].map(lambda p: p.get(key)), errors="coerce")


def question_rows(sessions: pd.DataFrame, client: pd.DataFrame, question_types: dict[str, str],
                  norms: dict | None = None) -> pd.DataFrame:
    """Cleaned client events (build_dataset.clean_events) -> one row per
    (session, opened question). `question_types`: question_id -> type.
    `norms`: question_norms.json content; None = compute from these rows
    (training). Shared with ml/inference."""
    ev = client.copy()
    ev["qid"] = ev["QuestionId"].where(ev["QuestionId"].notna(), ev["payload"].map(lambda p: p.get("questionId")))
    ev = ev[ev["qid"].notna()]
    key = ["SessionId", "qid"]

    views = ev[ev["EventType"] == "question_view_start"]
    first = views.groupby(key)["t_sec"].min().rename("first_view_t").reset_index()
    first["question_index"] = first.groupby("SessionId")["first_view_t"].rank(method="first").astype(int) - 1
    rows = first.set_index(key)

    def count(event_type: str, mask=None) -> pd.Series:
        sub = ev[ev["EventType"] == event_type]
        if mask is not None:   # .to_numpy(bool): an empty mask must stay a row filter
            sub = sub[mask(sub).to_numpy(dtype=bool)]
        return sub.groupby(key).size()

    def total(event_type: str, field: str, agg: str = "sum", scale: float = 1.0) -> pd.Series:
        sub = ev[ev["EventType"] == event_type]
        return _num(sub, field).groupby([sub["SessionId"], sub["qid"]]).agg(agg) / scale

    out = pd.DataFrame(index=rows.index)
    out["question_index"] = rows["question_index"]
    out["view_sec"] = total("question_view_end", "durationMs", scale=1000)
    out["view_count"] = count("question_view_start")
    out["blur_count"] = count("window_blur")
    out["blur_total_sec"] = total("window_focus", "blurDurationMs", scale=1000)
    out["blur_max_sec"] = total("window_focus", "blurDurationMs", "max", scale=1000)
    out["visibility_hidden_count"] = count("visibility_change", lambda d: d["payload"].map(lambda p: p.get("hidden") is True))
    for t in ("copy", "cut", "paste"):
        out[f"{t}_count"] = count(t)
    out["copy_chars"] = total("copy", "textLength")
    out["paste_chars"] = total("paste", "textLength")
    out["text_burst_count"] = count("text_burst_detected")
    out["text_burst_chars"] = total("text_burst_detected", "charDelta")
    prompt = lambda d: d["payload"].map(lambda p: p.get("zone") == "question_prompt")
    out["prompt_selected_count"] = count("text_selected", prompt)
    sel = ev[(ev["EventType"] == "text_selected")]
    sel = sel[prompt(sel).to_numpy(dtype=bool)]
    out["prompt_selected_chars"] = _num(sel, "selectionLength").groupby([sel["SessionId"], sel["qid"]]).sum()
    out["option_selected_count"] = count("text_selected", lambda d: d["payload"].map(lambda p: p.get("zone") == "question_option"))
    is_cp = lambda d: d["payload"].map(lambda p: p.get("shortcut") in COPY_PASTE_SHORTCUTS)
    out["shortcut_copy_paste_count"] = count("keyboard_shortcut", is_cp)
    out["shortcut_other_count"] = count("keyboard_shortcut", lambda d: ~is_cp(d).astype(bool))
    out["right_click_count"] = count("right_click")
    out["devtools_count"] = count("devtools_heuristic_triggered")
    out["window_resize_count"] = count("window_resize")
    out["multiple_tabs_count"] = count("multiple_tabs_detected")
    out["print_attempt_count"] = count("print_attempt")
    out["idle_count"] = count("idle_detected")
    out["idle_total_sec"] = total("idle_detected", "idleMs", scale=1000)
    out["answer_change_count"] = count("answer_change")
    out["answer_saved_count"] = count("answer_saved")

    counted = [c for c in out.columns if c not in ("question_index",)]
    out[counted] = out[counted].fillna(0)
    out["view_count"] = out["view_count"].clip(lower=1)
    out["revisits"] = out["view_count"] - 1
    out["blur_time_ratio"] = (out["blur_total_sec"] / out["view_sec"].replace(0, np.nan)).clip(upper=5)

    # typing, same burst rule as the session dataset
    ts = bd.typing_samples(client)
    ts = ts[ts["question_id"].notna()]
    clean = ts[(ts["is_burst"] == 0) & (ts["active_ms"] > 0)]
    tk, ck = [ts["session_id"], ts["question_id"]], [clean["session_id"], clean["question_id"]]
    typing = pd.DataFrame({
        "typing_samples_count": ts.groupby(tk).size(),
        "typing_chars": ts.groupby(tk)["chars_typed"].sum(),
        "typing_burst_samples": ts.groupby(tk)["is_burst"].sum(),
        "typing_max_active_cps": ts.groupby(tk)["active_cps"].max(),
        "typing_pause_ratio": ts.groupby(tk)["pause_ms"].sum() / ts.groupby(tk)["sample_ms"].sum().replace(0, np.nan),
        "typing_active_cps": clean.groupby(ck)["chars_typed"].sum() / (clean.groupby(ck)["active_ms"].sum() / 1000),
    })
    typing.index.names = key
    out = out.join(typing)
    out["typing_samples_count"] = out["typing_samples_count"].fillna(0)

    out = out.reset_index().rename(columns={"SessionId": "session_id", "qid": "question_id"})
    out["question_type"] = out["question_id"].map(question_types).fillna("unknown")
    out["is_free_text"] = (out["question_type"] == "free_text").astype(int)
    out["is_multiple_choice"] = (out["question_type"] == "multiple_choice").astype(int)

    # peer-relative features
    norms = norms if norms is not None else compute_norms(out)
    for col in ("view_sec", "answer_change_count"):
        by_q = out["question_id"].map(norms["by_question"].get(col, {}))
        by_type = out["question_type"].map(norms["by_type"].get(col, {}))
        median = by_q.fillna(by_type).fillna(norms["global"][col])
        out[col.replace("_count", "") + "_rel"] = (out[col] + 1) / (median + 1)

    out["rule_risk_score"] = np.clip(sum(w * out[col] for col, w in RULE_WEIGHTS.items()), 0, 100)
    return out


def compute_norms(rows: pd.DataFrame) -> dict:
    cols = ("view_sec", "answer_change_count")
    return {
        "note": "label-free medians over all students who opened the question",
        "by_question": {c: rows.groupby("question_id")[c].median().round(4).to_dict() for c in cols},
        "by_type": {c: rows.groupby("question_type")[c].median().round(4).to_dict() for c in cols},
        "global": {c: float(rows[c].median()) for c in cols},
    }


def main() -> None:
    report: dict = {}
    sessions = bd.load_sessions(report)
    client, _ = bd.load_events(sessions, report)
    questions = bd.read_raw("questions")
    types = dict(zip(questions["Id"], questions["Type"]))

    rows = question_rows(sessions, client, types)
    norms = compute_norms(rows)
    rows = question_rows(sessions, client, types, norms)   # same values, via the stored norms

    answers = bd.read_raw("student_answers")[["SessionId", "QuestionId", "AutoScore"]] \
        .rename(columns={"SessionId": "session_id", "QuestionId": "question_id", "AutoScore": "auto_score"})
    rows = rows.merge(answers, on=["session_id", "question_id"], how="left")
    meta = sessions[["session_id", "label", "group_id", "test"]].copy()
    meta["label"] = meta["label"].astype(int)
    rows = rows.merge(meta, on="session_id").sort_values(["session_id", "question_index"]).reset_index(drop=True)

    rows.to_csv(OUT_DIR / "questions.csv", index=False)
    (OUT_DIR / "question_norms.json").write_text(json.dumps(norms, indent=1))
    catalog = {
        "label": "label",
        "label_meaning": "WEAK label - the session's group label copied to each of its questions",
        "cv_group_column": "group_id",
        "meta": ["session_id", "question_id", "question_index", "question_type", "group_id", "test"],
        "excluded": {"rule_risk_score": "rule baseline per question", "auto_score": "outcome, test-dependent",
                     "print_attempt_count": "never observed in a question"},
        "features": QUESTION_FEATURES,
    }
    (OUT_DIR / "question_feature_catalog.json").write_text(json.dumps(catalog, indent=2))

    by = rows.groupby("label")
    print(f"questions: {len(rows)} rows from {rows['session_id'].nunique()} sessions "
          f"({by.size().get(0, 0)} natural / {by.size().get(1, 0)} cheating rows), {len(QUESTION_FEATURES)} features")
    print("mean per question, natural vs cheating:")
    for c in ("view_sec", "blur_total_sec", "paste_count", "text_burst_count", "prompt_selected_count"):
        m = by[c].mean()
        print(f"  mean {c:24s} {m.get(0):8.2f} {m.get(1):8.2f}")


if __name__ == "__main__":
    main()
