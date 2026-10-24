"""Voter #1 - CatBoost on session-level features (datasets/ml/sessions.csv).

Gradient-boosted decision trees with ordered boosting, which keeps
overfitting low on small tabular datasets like ours (310 sessions). Handles
missing values natively (sessions without typing / calibration keep NaN).

Steps:
  1. cross-validate through the shared harness (ml/evaluation/evaluate.py),
     both schemes (logo, lotto) - OOF predictions feed the ensemble later;
  2. refit on all sessions and save the model;
  3. export factor importance as mean |SHAP| per feature, plus its direction
     (does a higher value push towards "cheating" or away from it) - this is
     the "analysis of factors" part of the thesis title.

Hyperparameters are fixed and conservative on purpose, not tuned on the
outer CV folds (that would make the reported scores optimistic).

Run from the repo root:
    ml/.venv/bin/python ml/models/01_catboost/train.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from catboost import CatBoostClassifier, Pool

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (feature_columns, final_training_frame, load_table, run_cv,  # noqa: E402
                                 save_final_model)

MODEL_NAME = "01_catboost"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)

PARAMS = {
    "iterations": 500,
    "learning_rate": 0.03,
    "depth": 4,             # shallow trees: few samples, avoid memorising sessions
    "l2_leaf_reg": 5,
    "random_seed": 42,
    "verbose": 0,
    "allow_writing_files": False,
    "thread_count": -1,
}


def fit(train: pd.DataFrame, features: list[str]) -> dict:
    model = CatBoostClassifier(**PARAMS)
    model.fit(train[features], train["label"], sample_weight=train["sample_weight"])
    return {"model": model, "features": features}


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    return fitted["model"].predict_proba(test[fitted["features"]])[:, 1]


def make_fit_predict(features: list[str]):
    return lambda train, test: predict(fit(train, features), test)


def factor_importance(model: CatBoostClassifier, data: pd.DataFrame, features: list[str]) -> pd.DataFrame:
    shap = model.get_feature_importance(Pool(data[features]), type="ShapValues")[:, :-1]  # last col = bias
    rows = []
    for i, col in enumerate(features):
        values = data[col].to_numpy(dtype=float)
        ok = ~np.isnan(values)
        # Spearman between a feature's value and its SHAP contribution:
        # > 0 higher value -> more "cheating", < 0 -> less.
        # (undefined when the model never split on the feature: SHAP all 0)
        direction = (pd.Series(values[ok]).rank().corr(pd.Series(shap[ok, i]).rank())
                     if ok.sum() > 2 and np.nanstd(values) > 0 and shap[ok, i].std() > 0 else np.nan)
        rows.append({"feature": col, "mean_abs_shap": float(np.abs(shap[:, i]).mean()),
                     "direction": float(direction) if direction == direction else np.nan})
    out = pd.DataFrame(rows).sort_values("mean_abs_shap", ascending=False).reset_index(drop=True)
    out["share"] = out["mean_abs_shap"] / out["mean_abs_shap"].sum()
    return out


def main() -> None:
    features = feature_columns()
    print(f"{MODEL_NAME}: {len(features)} features")
    run_cv(MODEL_NAME, make_fit_predict(features), MODEL_DIR, params={**PARAMS, "n_features": len(features)})

    # Final model: same fit() as in CV, on all sessions -> saved + factor importance.
    final = fit(final_training_frame(), features)
    save_final_model(MODEL_NAME, final, predict, MODEL_DIR)

    out = MODEL_DIR / "results"
    (out / "features.json").write_text(json.dumps(features, indent=2))
    importance = factor_importance(final["model"], load_table("sessions"), features)
    importance.to_csv(out / "factor_importance.csv", index=False)

    print("\ntop 15 factors (mean |SHAP|, direction: + pushes towards cheating)")
    for _, r in importance.head(15).iterrows():
        arrow = "+" if r["direction"] > 0.1 else "-" if r["direction"] < -0.1 else "~"
        print(f"  {r['share']:6.1%}  {arrow}  {r['feature']}")

if __name__ == "__main__":
    main()
