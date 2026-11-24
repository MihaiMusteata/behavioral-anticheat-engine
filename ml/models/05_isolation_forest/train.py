"""Voter #5 - Isolation Forest, trained on NATURAL sessions only.

The only unsupervised voter: it never sees a cheating example. It learns
what normal exam behavior looks like and scores how hard a session is to
"isolate" from it with random splits (Liu, Ting & Zhou, ICDM 2008) - rare,
extreme sessions are isolated in few splits. Its value for the ensemble is
robustness: it does not depend on how the instructed "cheating" groups
happened to cheat, only on deviation from honest behavior.

Features (chosen a priori, not by looking at CV results): Isolation Forest
cannot down-weight irrelevant columns, so it gets a compact behavioral set:
  - every *_per_min rate (events, blur time, clipboard volume, idle time)
  - timing  (question view times, answered ratio, duration, navigation)
  - typing  (active / total speed, bursts, pauses)
Preprocessing: signed log1p (heavy tails would dominate the random split
points), median imputation (fitted on the natural training sessions).

Steps per CV fold:
  1. fit the forest on the training fold's natural sessions;
  2. anomaly score = -score_samples (higher = more anomalous);
  3. Platt scaling: logistic regression on the score over the whole training
     fold with balanced weights -> probability (the vote).

Run from the repo root:
    ml/.venv/bin/python ml/models/05_isolation_forest/train.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.ensemble import IsolationForest
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, load_catalog, run_cv, save_final_model,  # noqa: E402
                                 signed_log1p)

MODEL_NAME = "05_isolation_forest"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)

PARAMS = {"n_estimators": 500, "max_samples": "auto", "random_state": 42, "n_jobs": -1}


def compact_features() -> list[str]:
    fg = load_catalog()["feature_groups"]
    rates = [c for c in fg["behavior_rates"] if c.endswith("_per_min")]
    return rates + fg["timing"] + fg["typing"]


def fit(train: pd.DataFrame, features: list[str]) -> dict:
    x_train = signed_log1p(train[features].to_numpy(dtype=float))
    natural = train["label"].to_numpy() == 0
    imputer = SimpleImputer(strategy="median").fit(x_train[natural])
    forest = IsolationForest(**PARAMS).fit(imputer.transform(x_train[natural]))
    fitted = {"imputer": imputer, "forest": forest, "features": features}
    platt = LogisticRegression()
    platt.fit(_score(fitted, x_train), train["label"], sample_weight=train["sample_weight"].to_numpy())
    fitted["platt"] = platt
    return fitted


def _score(fitted: dict, x: np.ndarray) -> np.ndarray:
    """Anomaly score, higher = more anomalous than the natural sessions."""
    return -fitted["forest"].score_samples(fitted["imputer"].transform(x)).reshape(-1, 1)


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    x = signed_log1p(test[fitted["features"]].to_numpy(dtype=float))
    return fitted["platt"].predict_proba(_score(fitted, x))[:, 1]


def make_fit_predict(features: list[str]):
    return lambda train, test: predict(fit(train, features), test)


def main() -> None:
    features = compact_features()
    print(f"{MODEL_NAME}: {len(features)} features, forest fitted on natural sessions only")
    run_cv(MODEL_NAME, make_fit_predict(features), MODEL_DIR, params={**PARAMS, "n_features": len(features)})
    (MODEL_DIR / "results" / "features.json").write_text(json.dumps(features, indent=2))
    save_final_model(MODEL_NAME, fit(final_training_frame(), features), predict, MODEL_DIR)

if __name__ == "__main__":
    main()
