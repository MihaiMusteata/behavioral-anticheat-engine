"""Parity test: the live path (raw payload -> engine) must reproduce the
training path (build_dataset / build_question_dataset -> saved models) on
every historical session. If this fails, a model would see different numbers
in production than it was trained and evaluated on.

Checks, for all 310 sessions:
  1. session features from the payload == the row in datasets/ml/sessions.csv
  2. question features from the payload == the rows in datasets/ml/questions.csv
  3. each voter's probability via the engine == the saved model applied to
     the dataset row (sessions and questions)

Run from the repo root (~3 min, TabPFN dominates):
    ml/.venv/bin/python ml/inference/test_parity.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))  # ml/
from evaluation.evaluate import load_table, question_features  # noqa: E402
from inference.engine import DetectionEngine  # noqa: E402
from inference.features import payload_from_dump, payload_to_tables  # noqa: E402

TOL = 1e-6

# TabPFN v2 adds a "fingerprint" feature = a hash of each row's values, so a
# 1e-14 float round-off between the live computation and the CSV-stored
# dataset changes its output slightly (measured: max 0.002 on 40 sessions).
# Not a pipeline difference (features are checked identical above) - so for
# TabPFN the test allows 0.01 and requires that no vote flips at 0.5.
PRED_TOL = {"02_tabpfn": 0.01}


def compare(name: str, live: pd.DataFrame, offline: pd.DataFrame, cols: list[str]) -> None:
    a = live[cols].astype(float).to_numpy()
    b = offline[cols].astype(float).to_numpy()
    same = np.isclose(a, b, rtol=1e-9, atol=TOL, equal_nan=True)
    if not same.all():
        bad = [cols[j] for j in np.unique(np.where(~same)[1])]
        rows = np.unique(np.where(~same)[0])
        raise AssertionError(f"{name}: {len(rows)} rows differ in {bad[:10]}")
    print(f"OK  {name}: {a.shape[0]} rows x {a.shape[1]} columns identical")


def check_predictions(name: str, live: np.ndarray, offline: np.ndarray, tol: float) -> None:
    live, offline = np.asarray(live, dtype=float), np.asarray(offline, dtype=float)
    diff = np.abs(live - offline)
    diff[np.isnan(live) & np.isnan(offline)] = 0
    flips = int(((live >= 0.5) != (offline >= 0.5))[np.isfinite(live) & np.isfinite(offline)].sum())
    if np.isnan(diff).any() or diff.max() > tol or flips:
        raise AssertionError(f"{name}: max diff {np.nanmax(diff):.5f} (tol {tol}), vote flips {flips}")
    print(f"OK  {name}: {np.isfinite(live).sum()} rows, max diff {diff.max():.1e} (tol {tol:g}), vote flips 0")


def main() -> None:
    engine = DetectionEngine()
    sessions = load_table("sessions")
    questions = load_table("questions")
    ids = sessions["session_id"].tolist()
    payloads = [payload_from_dump(sid) for sid in ids]

    tables = [payload_to_tables(p, engine.norms) for p in payloads]
    live_s = pd.concat([t["session"] for t in tables], ignore_index=True).set_index("session_id").loc[ids]
    live_q = pd.concat([t["questions"] for t in tables], ignore_index=True)

    # 1. session features used by any voter
    used = sorted({c for _, (m, f) in engine.session_voters.items() if isinstance(f, dict) and "features" in f
                   for c in f["features"]})
    compare("session features", live_s.reset_index(), sessions.set_index("session_id").loc[ids].reset_index(), used)

    # 2. question features
    key = ["session_id", "question_id"]
    off_q = questions.set_index(key)
    live_q = live_q.set_index(key)
    assert set(live_q.index) == set(off_q.index), "question rows differ"
    compare("question features", live_q.loc[off_q.index].reset_index(), off_q.reset_index(), question_features())

    # 3. predictions: engine vs saved model on the dataset rows
    results = engine.predict(payloads)
    for name, (module, fitted) in engine.session_voters.items():
        if name == "04_minirocket":
            module._panel = module.build_panel()[0]
        offline = module.predict(fitted, sessions)
        live = [r["session"]["models"][name]["probability"] for r in results]
        live = np.array([np.nan if v is None else v for v in live])
        check_predictions(f"session predictions {name}", live, offline, PRED_TOL.get(name, 1e-4))
    for name, (module, fitted) in engine.question_voters.items():
        offline = pd.Series(module.predict(fitted, questions), index=pd.MultiIndex.from_frame(questions[key]))
        live = pd.Series({(r["sessionId"], q["questionId"]): q["models"][name]["probability"]
                          for r in results for q in r["questions"]}, dtype=float)
        check_predictions(f"question predictions {name}", live.loc[offline.index].to_numpy(), offline.to_numpy(),
                          PRED_TOL.get(name, 1e-4))
    print("\nPARITY OK - the live path reproduces the training data and the saved models exactly")


if __name__ == "__main__":
    main()
