"""Baseline - the platform's current rule-based risk score (no machine learning).

`rule_risk_score` is what the Worker computes today from the weighted rules
in appsettings.json -> RiskScoring:Rules (fixed, hand-chosen weights per
event type). It is the reference every AI voter has to beat.

Nothing is learned: the score (0-100, capped) maps to a probability with a
FIXED transform, p = score / 100, identical in every fold. A per-fold fitted
calibration (Platt) would be wrong here: it is monotonic *within* a fold, but
pooling 13 differently-calibrated folds reorders sessions across folds and
artificially lowered the pooled ROC-AUC to 0.742 vs the true 0.810 of the raw
score (found 2026-11-08). With a fixed transform the reported AUC is exactly
the raw score's.

Finding: the score saturates - 81% of cheating AND 28% of natural sessions
sit at the cap (100), so no threshold separates them there; that is why the
rules catch 0% of cheaters at <= 10% false accusations.

Run from the repo root:
    ml/.venv/bin/python ml/baselines/rule_based_score/train.py
"""

from __future__ import annotations

import sys
from pathlib import Path

import numpy as np
import pandas as pd

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import final_training_frame, run_cv, save_final_model  # noqa: E402

MODEL_NAME = "00_rule_based_score"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)


SCORE_CAP = 100.0


def fit(train: pd.DataFrame) -> dict:
    return {"transform": "rule_risk_score / 100", "score_cap": SCORE_CAP}   # nothing to learn


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    return np.clip(test["rule_risk_score"].to_numpy(dtype=float) / fitted["score_cap"], 0.0, 1.0)


def main() -> None:
    print(f"{MODEL_NAME}: current rule-based risk score, fixed transform score/100")
    run_cv(MODEL_NAME, lambda train, test: predict(fit(train), test), MODEL_DIR,
           params={"source": "appsettings.json -> RiskScoring:Rules", "transform": "score / 100 (fixed, not fitted)"})
    save_final_model(MODEL_NAME, fit(final_training_frame()), predict, MODEL_DIR)


if __name__ == "__main__":
    main()
