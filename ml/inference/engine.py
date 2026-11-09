"""Detection engine - loads the trained voters + the voting configuration and
turns raw session payloads (see features.py) into the JSON the platform
shows: every model's probability (monitoring) + the ensemble score (final),
for the whole session and for every question.

    from inference.engine import DetectionEngine
    engine = DetectionEngine()               # loads everything once (~30 s, TabPFN cache)
    result = engine.predict([payload])[0]    # list in, list out (batching is faster)

Output per session:
{
  "sessionId": "...",
  "session": {
    "models":   {"01_catboost": {"probability": 0.93, "vote": "suspect"}, ...},   # 5 voters
    "ensemble": {"score": 0.91, "decision": "suspect", "level": "high",
                 "method": "soft_weighted", "votersUsed": 5}
  },
  "questions": [
    {"questionId": "...", "questionIndex": 0, "questionType": "free_text",
     "models":   {"01_catboost": {...}, ...},                                    # 4 voters
     "ensemble": {"score": 0.88, "decision": "suspect", "level": "medium", ...},
     "evidence": {"viewSec": 24.3, "blurTotalSec": 19.0, "pasteChars": 38, ...}}
  ],
  "questionSummary": {"top3MeanScore": 0.86, "suspectQuestions": 9, "totalQuestions": 15},
  "notes": [...]
}
A voter that cannot score an input returns null (probability) and is left
out of the vote (e.g. MiniRocket with < 9 heartbeats).
"""

from __future__ import annotations

import importlib.util
import json
import sys
import warnings
from pathlib import Path

import joblib
import numpy as np
import pandas as pd

ML_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ML_DIR))   # pickled models reference evaluation.* helpers
from ensemble.voting import decide, soft_vote  # noqa: E402
from inference.features import payload_to_tables  # noqa: E402

DATA_DIR = ML_DIR.parent / "datasets" / "ml"

EVIDENCE = {   # question column -> output key shown in the audit UI
    "view_sec": "viewSec", "view_sec_rel": "viewSecVsPeers", "revisits": "revisits",
    "blur_count": "tabSwitches", "blur_total_sec": "blurTotalSec", "blur_max_sec": "blurMaxSec",
    "paste_count": "pasteCount", "paste_chars": "pasteChars",
    "text_burst_count": "textBurstCount", "text_burst_chars": "textBurstChars",
    "copy_count": "copyCount", "prompt_selected_count": "promptSelections",
    "shortcut_copy_paste_count": "copyPasteShortcuts", "devtools_count": "devtoolsSignals",
    "answer_change_count": "answerChanges", "typing_active_cps": "typingCharsPerSec",
    "typing_max_active_cps": "typingMaxCharsPerSec",
}


def _module(path: Path, name: str):
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _vote(p: float, threshold: float) -> dict:
    if p is None or np.isnan(p):
        return {"probability": None, "vote": "abstain"}
    return {"probability": round(float(p), 4), "vote": "suspect" if p >= threshold else "not_suspect"}


def _clean(v):
    if v is None or (isinstance(v, (float, np.floating)) and np.isnan(v)):
        return None
    if isinstance(v, (float, np.floating)):
        return int(v) if float(v).is_integer() else round(float(v), 3)   # counts as 1, not 1.0
    return int(v)


class DetectionEngine:
    def __init__(self, ml_dir: Path = ML_DIR, norms_path: Path = DATA_DIR / "question_norms.json",
                 skip_voters: dict[str, list[str]] | None = None):
        """skip_voters: e.g. {"question": ["02_tabpfn"]} to trade a voter for
        speed (TabPFN at question level costs ~33 s per session on CPU). The
        vote renormalises its weights over the remaining voters."""
        warnings.filterwarnings("ignore")
        self.config = json.loads((ml_dir / "ensemble" / "results" / "ensemble_config.json").read_text())
        for level, names in (skip_voters or {}).items():
            cfg = self.config[level]
            cfg["voters"] = [v for v in cfg["voters"] if v not in names]
            cfg["weights"] = {k: w for k, w in cfg["weights"].items() if k not in names}
            cfg["skipped_voters"] = list(names)
        self.norms = json.loads(norms_path.read_text())
        models_dir = ml_dir / "models"
        self.session_voters, self.question_voters = {}, {}
        for name in self.config["session"]["voters"]:
            module = _module(models_dir / name / "train.py", f"voter_{name}")
            fitted = joblib.load(models_dir / name / "artifacts" / "model.joblib")
            if "rocket" in fitted:
                # Saved with n_jobs=-1 on the training machine; sktime resolves
                # that to os.cpu_count(), which crashes numba when fewer threads
                # are available here (container CPU limits, NUMBA_NUM_THREADS).
                # Thread count only affects speed, never the output.
                import numba
                fitted["rocket"].n_jobs = numba.config.NUMBA_NUM_THREADS
            self.session_voters[name] = (module, fitted)
        for name in self.config["question"]["voters"]:
            module = self.session_voters[name][0] if name in self.session_voters else \
                _module(models_dir / name / "train.py", f"voter_{name}")
            fitted = joblib.load(models_dir / name / "artifacts" / "question_model.joblib")
            self.question_voters[name] = (module, fitted)

    # ------------------------------------------------------------------ api

    def predict(self, payloads: list[dict]) -> list[dict]:
        tables = [payload_to_tables(p, self.norms) for p in payloads]
        sessions = pd.concat([t["session"] for t in tables], ignore_index=True)
        heartbeats = pd.concat([t["heartbeats"] for t in tables], ignore_index=True)
        questions = pd.concat([t["questions"] for t in tables if len(t["questions"])], ignore_index=True) \
            if any(len(t["questions"]) for t in tables) else pd.DataFrame()

        s_probs = self._session_probabilities(sessions, heartbeats)
        q_probs = self._question_probabilities(questions) if len(questions) else pd.DataFrame()
        return [self._assemble(sid, s_probs, questions, q_probs) for sid in sessions["session_id"]]

    # ------------------------------------------------------------- internals

    def _session_probabilities(self, sessions: pd.DataFrame, heartbeats: pd.DataFrame) -> pd.DataFrame:
        out = pd.DataFrame(index=sessions["session_id"])
        for name, (module, fitted) in self.session_voters.items():
            if name == "04_minirocket":
                module._panel = module.build_panel(heartbeats)[0]   # this batch's series only
            out[name] = module.predict(fitted, sessions)
        return out

    def _question_probabilities(self, questions: pd.DataFrame) -> pd.DataFrame:
        out = pd.DataFrame(index=questions.index)
        for name, (module, fitted) in self.question_voters.items():
            out[name] = module.predict(fitted, questions)
        return out

    def _assemble(self, sid: str, s_probs: pd.DataFrame, questions: pd.DataFrame, q_probs: pd.DataFrame) -> dict:
        sc, qc = self.config["session"], self.config["question"]
        row = s_probs.loc[[sid]]
        score = float(soft_vote(row, sc["weights"]).iloc[0])
        session = {
            "models": {n: _vote(row[n].iloc[0], sc["threshold"]) for n in sc["voters"]},
            "ensemble": {"score": None if np.isnan(score) else round(score, 4),
                         **decide(score, sc["threshold"], sc["strict_threshold"]),
                         "method": sc["method"], "votersUsed": int(row.notna().sum(axis=1).iloc[0])},
        }

        qs = []
        if len(questions):
            mask = questions["session_id"] == sid
            qrows, qp = questions[mask], q_probs[mask]
            qscore = soft_vote(qp, qc["weights"])
            for i in qrows.sort_values("question_index").index:
                r = qrows.loc[i]
                s = float(qscore.loc[i])
                qs.append({
                    "questionId": r["question_id"], "questionIndex": int(r["question_index"]),
                    "questionType": r["question_type"],
                    "models": {n: _vote(qp.loc[i, n], qc["threshold"]) for n in qc["voters"]},
                    "ensemble": {"score": None if np.isnan(s) else round(s, 4),
                                 **decide(s, qc["threshold"], qc["strict_threshold"]), "method": qc["method"]},
                    "evidence": {key: _clean(r[col]) for col, key in EVIDENCE.items() if col in r},
                })
        scores = pd.Series([q["ensemble"]["score"] for q in qs], dtype=float)
        return {
            "sessionId": sid,
            "session": session,
            "questions": qs,
            "questionSummary": {
                "top3MeanScore": None if scores.empty else round(float(scores.nlargest(3).mean()), 4),
                "suspectQuestions": int((scores >= qc["threshold"]).sum()),
                "totalQuestions": len(qs),
            },
            "notes": [self.config["prior_note"],
                      "Question scores use weak labels (session label per question): read them as "
                      "'where to look', not as proof for a single question."],
        }
