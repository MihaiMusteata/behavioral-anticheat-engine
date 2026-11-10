"""Voter #2 - TabPFN v2 on session-level features (datasets/ml/sessions.csv).

TabPFN (Hollmann et al., Nature 2025) is a transformer pre-trained on
millions of synthetic tabular datasets. It does not learn weights from our
data: the training fold is given as context and the prediction is a single
forward pass ("in-context learning"). It is designed for exactly our scale
(hundreds of rows, <500 features) and handles missing values natively.

Version pinned to tabpfn==2.0.9 - the open-weights v2 model from the paper.
Later releases ship newer checkpoints under a different license.

Class balance: TabPFN takes no sample_weight, so the harness's balanced
weights cannot be passed. `balance_probabilities=True` divides the output by
the training-fold class prior instead - same effect (prior pinned to 0.5),
which avoids the fold-prior leak described in ml/evaluation/evaluate.py.

The saved final model is the classifier "fitted" on all sessions - for
TabPFN that means the pre-trained weights plus all 310 sessions as context;
factor importance is left to the interpretable voters (CatBoost SHAP,
logistic regression coefficients).

Run from the repo root (~5 min on CPU):
    ml/.venv/bin/python ml/models/02_tabpfn/train.py
"""

from __future__ import annotations

import json
import sys
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
from tabpfn import TabPFNClassifier

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import feature_columns, final_training_frame, run_cv, save_final_model  # noqa: E402

MODEL_NAME = "02_tabpfn"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)

PARAMS = {
    "n_estimators": 4,                # library default: 4 preprocessing/feature-shuffle ensembles
    "balance_probabilities": True,    # replaces sample_weight, see docstring
    "device": "cpu",                  # deterministic; no GPU on this machine
    "random_state": 42,
}


def fit(train: pd.DataFrame, features: list[str]) -> dict:
    x, y = train[features].to_numpy(dtype=float), train["label"].to_numpy()
    model = TabPFNClassifier(**PARAMS)
    model.fit(x, y)
    # The training context is kept so ml-service can rebuild the model with
    # fit_mode="fit_with_cache" at startup (identical predictions, ~0.3 s per
    # call instead of ~14-55 s on CPU) without pickling the 0.5 GB cache.
    return {"model": model, "features": features, "x_train": x, "y_train": y, "params": PARAMS}


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    return fitted["model"].predict_proba(test[fitted["features"]].to_numpy(dtype=float))[:, 1]


def make_fit_predict(features: list[str]):
    return lambda train, test: predict(fit(train, features), test)


def main() -> None:
    warnings.filterwarnings("ignore", message=".*GPU.*")
    features = feature_columns()
    print(f"{MODEL_NAME}: {len(features)} features")
    run_cv(MODEL_NAME, make_fit_predict(features), MODEL_DIR, params={**PARAMS, "n_features": len(features)})
    (MODEL_DIR / "results" / "features.json").write_text(json.dumps(features, indent=2))
    save_final_model(MODEL_NAME, fit(final_training_frame(), features), predict, MODEL_DIR)

if __name__ == "__main__":
    main()
