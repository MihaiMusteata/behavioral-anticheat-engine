"""Voter #6 - deviation from the student's OWN typing baseline (calibration).

Every other voter compares a student with the population. This one compares
the student with themselves: the join screen measured how fast they type
their name and specialty (text nobody copies), and the exam measures how fast
text appears in their free_text answers. Text appearing much faster than the
student can physically type is pasted / dictated / AI-generated.

Method: one-sided scaled Manhattan distance (Killourhy & Maxion, DSN 2009 -
best-performing simple detector in their keystroke-dynamics benchmark).

  1. Per session, label-free deviation features from the ratio
     r = exam active chars/sec of a typing sample / the student's calibration
     chars/sec:
       log_clean_ratio       log(overall exam speed / baseline), samples with
                             r <= 3 only (the "really typed" part)
       log_p90_ratio         log of the 90th percentile of r
       share_samples_gt2     share of samples typed > 2x own baseline
       share_samples_gt3     share of samples typed > 3x own baseline
       share_chars_gt3       share of exam characters in samples > 3x baseline
  2. Template = mean of these features over the NATURAL sessions of the
     training fold, scale = their mean absolute deviation (as in K&M).
     distance = sum_i max(0, x_i - mean_i) / mad_i   - one-sided: only
     "faster than your own baseline" counts, slower is normal (thinking).
  3. Platt scaling: logistic regression on log1p(distance), fitted on the
     training fold with balanced weights -> probability (the vote).

Coverage: needs both a calibration sample and free_text typing samples;
other sessions get no vote (NaN) - by design, the ensemble decides there.

Ablation (2026-11-06, 216 covered sessions): replacing each student's own
baseline with the population median calibration speed gives the same
ROC-AUC (logo 0.754 vs 0.750, lotto 0.749 vs 0.735 - within noise). With a
single short calibration (name + specialty), the personal baseline adds no
measurable value yet; the signal is "text appears faster than people type".
The voter is still the least correlated with the tabular ones (Spearman
0.46 vs CatBoost), which is what an ensemble needs.

Run from the repo root:
    ml/.venv/bin/python ml/archive/06_calibration_deviation/train.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, load_table,  # noqa: E402
                                 run_cv, save_final_model)

MODEL_NAME = "06_calibration_deviation"
MODEL_DIR = Path(__file__).resolve().parent   # archived voter - results/ + artifacts/ here

PERSONAL_BURST_RATIO = 3.0   # > 3x own speed: not plausibly typed
FAST_RATIO = 2.0
MAD_FLOOR = 1e-3             # a feature constant across natural sessions must not divide by 0

DEVIATION_FEATURES = ["log_clean_ratio", "log_p90_ratio", "share_samples_gt2",
                      "share_samples_gt3", "share_chars_gt3"]

PARAMS = {"personal_burst_ratio": PERSONAL_BURST_RATIO, "fast_ratio": FAST_RATIO,
          "features": DEVIATION_FEATURES, "distance": "one-sided scaled Manhattan (Killourhy & Maxion 2009)",
          "template": "natural sessions of the training fold", "calibration": "Platt on log1p(distance)"}


# ------------------------------------------------------------------ stage 1

def deviation_features() -> pd.DataFrame:
    sessions = load_table("sessions")[["session_id", "calib_cps"]].dropna()
    ts = load_table("typing_samples")
    ts = ts[(ts["active_ms"] > 0) & (ts["chars_typed"] > 0)].merge(sessions, on="session_id")
    ts["ratio"] = ts["active_cps"] / ts["calib_cps"]

    rows = []
    for sid, d in ts.groupby("session_id"):
        clean = d[d["ratio"] <= PERSONAL_BURST_RATIO]
        clean_cps = clean["chars_typed"].sum() / (clean["active_ms"].sum() / 1000) if len(clean) else np.nan
        burst = d["ratio"] > PERSONAL_BURST_RATIO
        rows.append({
            "session_id": sid,
            "n_samples": len(d),
            "log_clean_ratio": np.log(clean_cps / d["calib_cps"].iloc[0]) if clean_cps == clean_cps else 0.0,
            "log_p90_ratio": float(np.log(d["ratio"].quantile(0.9))),
            "share_samples_gt2": float((d["ratio"] > FAST_RATIO).mean()),
            "share_samples_gt3": float(burst.mean()),
            "share_chars_gt3": float(d.loc[burst, "chars_typed"].sum() / d["chars_typed"].sum()),
        })
    return pd.DataFrame(rows)


# ------------------------------------------------------------------ stage 2

def scaled_manhattan(x: np.ndarray, template: np.ndarray, mad: np.ndarray) -> np.ndarray:
    return (np.maximum(0.0, x - template) / mad).sum(axis=1)


def fit(train: pd.DataFrame, table: pd.DataFrame) -> dict:
    """table = deviation features indexed by session_id (see deviation_features)."""
    train = train[train["session_id"].isin(table.index)]
    x_train = table.loc[train["session_id"], DEVIATION_FEATURES].to_numpy()
    natural = x_train[train["label"].to_numpy() == 0]
    template = natural.mean(axis=0)
    mad = np.maximum(np.abs(natural - template).mean(axis=0), MAD_FLOOR)
    dist = np.log1p(scaled_manhattan(x_train, template, mad)).reshape(-1, 1)
    platt = LogisticRegression()
    platt.fit(dist, train["label"], sample_weight=train["sample_weight"].to_numpy())
    return {"template": template, "mad": mad, "platt": platt, "features": DEVIATION_FEATURES}


def predict(fitted: dict, test: pd.DataFrame, table: pd.DataFrame) -> np.ndarray:
    out = np.full(len(test), np.nan)
    has = test["session_id"].isin(table.index).to_numpy()
    if has.any():
        x = table.loc[test.loc[has, "session_id"], fitted["features"]].to_numpy()
        dist = np.log1p(scaled_manhattan(x, fitted["template"], fitted["mad"])).reshape(-1, 1)
        out[has] = fitted["platt"].predict_proba(dist)[:, 1]
    return out


def make_fit_predict(dev: pd.DataFrame):
    table = dev.set_index("session_id")
    return lambda train, test: predict(fit(train, table), test, table)


def main() -> None:
    dev = deviation_features()
    total = len(load_table("sessions"))
    print(f"{MODEL_NAME}: {len(dev)} / {total} sessions have calibration + free_text typing "
          f"(no vote for the other {total - len(dev)})")

    out = MODEL_DIR / "results"
    out.mkdir(parents=True, exist_ok=True)
    (MODEL_DIR / "artifacts").mkdir(parents=True, exist_ok=True)
    dev.to_csv(MODEL_DIR / "artifacts" / "deviation_features.csv", index=False)   # per-session -> gitignored

    run_cv(MODEL_NAME, make_fit_predict(dev), MODEL_DIR, params=PARAMS)
    (out / "features.json").write_text(json.dumps(DEVIATION_FEATURES, indent=2))
    table = dev.set_index("session_id")
    save_final_model(MODEL_NAME, fit(final_training_frame(), table),
                     lambda fitted, test: predict(fitted, test, table), MODEL_DIR)

    labels = load_table("sessions").set_index("session_id")["label"]
    d = dev.set_index("session_id").join(labels)
    print("\ndeviation features, median natural vs cheating (ratio = exam / own calibration)")
    for c in DEVIATION_FEATURES:
        m = d.groupby("label")[c].median()
        shown = (lambda v: f"{np.exp(v):6.2f}x") if c.startswith("log_") else (lambda v: f"{v:7.3f}")
        print(f"  {c:20s} {shown(m.get(0))}  {shown(m.get(1))}")


if __name__ == "__main__":
    main()
