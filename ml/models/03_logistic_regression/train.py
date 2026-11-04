"""Voter #3 - L2-regularised logistic regression on session-level features.

The simplest, most interpretable voter: one weight per feature, so each
coefficient reads directly as "how much does this factor raise the odds of
cheating". It also serves as the reference the heavier models must beat.

Preprocessing (fitted on the training fold only, inside the pipeline):
  1. signed log1p - counts/durations are heavily right-skewed (mostly 0, a
     few very large); a linear model would otherwise be driven by outliers;
  2. median imputation + missing-value indicators - sessions without typing /
     calibration keep the information that the value was missing;
  3. standardisation - so coefficients are comparable (effect per 1 SD).

C is fixed a priori (strong regularisation, many correlated features such as
a count and its per-minute rate), not tuned on the outer CV folds.

Run from the repo root:
    ml/.venv/bin/python ml/models/03_logistic_regression/train.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegression
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import FunctionTransformer, StandardScaler

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (feature_columns, final_training_frame, run_cv,  # noqa: E402
                                 save_final_model, signed_log1p)

MODEL_NAME = "03_logistic_regression"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)

PARAMS = {"C": 0.1, "penalty": "l2", "solver": "lbfgs", "max_iter": 5000}


def make_pipeline() -> Pipeline:
    return Pipeline([
        ("log", FunctionTransformer(signed_log1p, feature_names_out="one-to-one")),
        ("impute", SimpleImputer(strategy="median", add_indicator=True, keep_empty_features=False)),
        ("scale", StandardScaler()),
        ("clf", LogisticRegression(**PARAMS)),
    ])


def fit(train: pd.DataFrame, features: list[str]) -> dict:
    model = make_pipeline()
    model.fit(train[features], train["label"], clf__sample_weight=train["sample_weight"].to_numpy())
    return {"model": model, "features": features}


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    return fitted["model"].predict_proba(test[fitted["features"]])[:, 1]


def make_fit_predict(features: list[str]):
    return lambda train, test: predict(fit(train, features), test)


def coefficients(model: Pipeline) -> pd.DataFrame:
    names = model[:-1].get_feature_names_out()
    coef = model.named_steps["clf"].coef_[0]
    out = pd.DataFrame({"feature": names, "coef_per_sd": coef, "odds_ratio_per_sd": np.exp(coef)})
    out["feature"] = out["feature"].str.replace("missingindicator_", "missing:", regex=False)
    return out.reindex(out["coef_per_sd"].abs().sort_values(ascending=False).index).reset_index(drop=True)


def main() -> None:
    features = feature_columns()
    print(f"{MODEL_NAME}: {len(features)} features")
    run_cv(MODEL_NAME, make_fit_predict(features), MODEL_DIR, params={**PARAMS, "n_features": len(features)})

    final = fit(final_training_frame(), features)
    save_final_model(MODEL_NAME, final, predict, MODEL_DIR)

    out = MODEL_DIR / "results"
    (out / "features.json").write_text(json.dumps(features, indent=2))
    coefs = coefficients(final["model"])
    coefs.to_csv(out / "coefficients.csv", index=False)

    print("\ntop 15 coefficients (odds ratio per +1 SD of the log-scaled feature)")
    for _, r in coefs.head(15).iterrows():
        print(f"  OR {r['odds_ratio_per_sd']:5.2f}  {'+' if r['coef_per_sd'] > 0 else '-'}  {r['feature']}")

if __name__ == "__main__":
    main()
