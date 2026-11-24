"""Voter #5 - ARIMAX on the heartbeat time series (datasets/ml/heartbeats.csv).

ARIMAX is a forecasting model, not a classifier, so it votes through a
two-stage design ("model-based feature extraction"):

  Stage 1 - per session, label-free. Each session gets its own
    ARIMAX(1,0,1) on its heartbeat series:
      endogenous  y_t = log1p(idle seconds at heartbeat t)
      exogenous   x_t = behavioral events that happened in the interval
                  (t-1, t]: window_blur, paste, text_burst_detected,
                  answer_change, question_navigation  (increments of the
                  leak-free running counts)
    plus a plain ARIMA(1,0,1) without exog, as reference. From the fits:
      - exog coefficients: how strongly each event type moves inactivity
        in *this* student's session;
      - ar / ma / sigma2: the dynamics of the student's inactivity;
      - aic_gain = AIC(ARIMA) - AIC(ARIMAX): how much the behavioral events
        explain beyond the series' own past (the ARIMA vs ARIMAX question);
      - residual anomaly stats (standardised one-step forecast errors):
        max |z|, share of |z| > 2 - the classic "forecast residual ->
        anomaly score" use of ARIMA.
    Stage 1 never sees labels or other sessions, so it is computed once,
    outside the CV loop, without leakage.

  Stage 2 - inside the CV harness: median-impute + standardise + L2 logistic
    regression on the Stage-1 features, C chosen by inner group-CV within
    the training fold, balanced weights. Output = probability (the vote).

Exog is limited to 5 event types on purpose: sessions have ~50 heartbeats,
more regressors would over-parameterise each fit. Sessions shorter than
MIN_POINTS heartbeats get no vote (NaN).

Run from the repo root:
    ml/.venv/bin/python ml/archive/05_arimax/train.py
"""

from __future__ import annotations

import json
import sys
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
from joblib import Parallel, delayed
from sklearn.impute import SimpleImputer
from sklearn.linear_model import LogisticRegressionCV
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, inner_group_splits,  # noqa: E402
                                 load_table, run_cv, save_final_model)

MODEL_NAME = "05_arimax"
MODEL_DIR = Path(__file__).resolve().parent   # archived voter - results/ + artifacts/ here

ORDER = (1, 0, 1)
MIN_POINTS = 15
EXOG_EVENTS = ["window_blur", "paste", "text_burst_detected", "answer_change", "question_navigation"]
EXCLUDED_FEATURES = ["exog_active_count"]

PARAMS = {
    "order": list(ORDER),
    "endog": "log1p(idle_ms / 1000)",
    "exog": EXOG_EVENTS,
    "min_points": MIN_POINTS,
    "excluded_features": ["exog_active_count"],
    "Cs": np.logspace(-4, 2, 13).tolist(),
    "inner_folds": 4,
}


# ------------------------------------------------------------------ stage 1

def _fit(endog: np.ndarray, exog: np.ndarray | None):
    from statsmodels.tsa.statespace.sarimax import SARIMAX
    with warnings.catch_warnings():
        warnings.simplefilter("ignore")   # convergence chatter on short series
        model = SARIMAX(endog, exog=exog, order=ORDER, trend="c")
        return model.fit(disp=False, maxiter=200)


def session_params(session_id: str, d: pd.DataFrame) -> dict:
    out = {"session_id": session_id, "n_points": len(d)}
    if len(d) < MIN_POINTS:
        return out
    y = np.log1p(d["idle_ms"].clip(lower=0).fillna(0).to_numpy() / 1000)
    running = d[[f"{e}_so_far" for e in EXOG_EVENTS]].to_numpy(dtype=float)
    increments = np.diff(running, axis=0, prepend=np.zeros((1, running.shape[1])))
    active = increments.std(axis=0) > 0     # a constant column would be collinear with the trend
    try:
        base = _fit(y, None)
        full = _fit(y, increments[:, active]) if active.any() else base
    except Exception:  # numerical failure on a degenerate series
        return out

    names = list(full.model.param_names)
    params = dict(zip(names, full.params))
    exog_names = [n for n in names if n not in ("intercept", "ar.L1", "ma.L1", "sigma2")]
    coef = dict(zip([e for e, a in zip(EXOG_EVENTS, active) if a], [params[n] for n in exog_names]))
    for e in EXOG_EVENTS:
        out[f"coef_{e}"] = coef.get(e, 0.0)          # event never happened -> no effect
    out["exog_active_count"] = int(active.sum())
    out["ar_l1"] = params.get("ar.L1", np.nan)
    out["ma_l1"] = params.get("ma.L1", np.nan)
    out["sigma2"] = params.get("sigma2", np.nan)
    out["intercept"] = params.get("intercept", np.nan)
    out["aic_arima"] = base.aic
    out["aic_arimax"] = full.aic
    out["aic_gain"] = base.aic - full.aic
    z = full.resid[1:] / np.sqrt(max(params.get("sigma2", np.nan), 1e-12))
    out["resid_max_abs_z"] = float(np.max(np.abs(z)))
    out["resid_share_abs_z_gt2"] = float(np.mean(np.abs(z) > 2))
    out["resid_mean_z"] = float(np.mean(z))
    return out


def extract_all() -> pd.DataFrame:
    hb = load_table("heartbeats").sort_values(["session_id", "hb_index"])
    rows = Parallel(n_jobs=-1)(delayed(session_params)(sid, d) for sid, d in hb.groupby("session_id"))
    return pd.DataFrame(rows)


# ------------------------------------------------------------------ stage 2

def fit(train: pd.DataFrame, table: pd.DataFrame, features: list[str]) -> dict:
    """table = Stage-1 parameters indexed by session_id (see extract_all)."""
    fitted_ids = table.dropna(subset=["aic_arimax"]).index
    train = train[train["session_id"].isin(fitted_ids)].reset_index(drop=True)
    clf = Pipeline([
        ("impute", SimpleImputer(strategy="median")),
        ("scale", StandardScaler()),
        ("clf", LogisticRegressionCV(Cs=PARAMS["Cs"], cv=inner_group_splits(train, PARAMS["inner_folds"]),
                                     scoring="roc_auc", max_iter=5000)),
    ])
    clf.fit(table.loc[train["session_id"], features].to_numpy(), train["label"],
            clf__sample_weight=train["sample_weight"].to_numpy())
    # Stage 1 is per session: a new session's parameters come from
    # session_params(session_id, its heartbeats) with the same ORDER/EXOG.
    return {"clf": clf, "features": features, "order": ORDER, "exog_events": EXOG_EVENTS,
            "min_points": MIN_POINTS}


def predict(fitted: dict, test: pd.DataFrame, table: pd.DataFrame) -> np.ndarray:
    fitted_ids = table.dropna(subset=["aic_arimax"]).index
    has = test["session_id"].isin(fitted_ids).to_numpy()
    out = np.full(len(test), np.nan)
    if has.any():
        x = table.loc[test.loc[has, "session_id"], fitted["features"]].to_numpy()
        out[has] = fitted["clf"].predict_proba(x)[:, 1]
    return out


def make_fit_predict(params: pd.DataFrame, features: list[str]):
    table = params.set_index("session_id")
    return lambda train, test: predict(fit(train, table, features), test, table)


def main() -> None:
    params = extract_all()
    fitted = params["aic_arimax"].notna()
    print(f"{MODEL_NAME}: ARIMAX{ORDER} fitted on {fitted.sum()} / {len(params)} sessions "
          f"(< {MIN_POINTS} heartbeats or failed fit: {(~fitted).sum()})")

    out = MODEL_DIR / "results"
    out.mkdir(parents=True, exist_ok=True)
    (MODEL_DIR / "artifacts").mkdir(parents=True, exist_ok=True)
    params.to_csv(MODEL_DIR / "artifacts" / "session_params.csv", index=False)   # per-session -> gitignored

    # exog_active_count (how many event types occurred) is kept in
    # session_params.csv but NOT voted on: it is a plain count that alone
    # scores ROC-AUC 0.91 under logo - i.e. it would smuggle tabular
    # information already used by voters #1-#3 into the "time-series" voter
    # and fake ensemble diversity. Ablation (2026-11-06, 310 sessions):
    #   all params + count               logo 0.864  lotto 0.889
    #   count only                       logo 0.911  lotto 0.706
    #   exog coefs + dynamics (default)  logo 0.731  lotto 0.781
    #   ARIMA dynamics + residuals only  logo 0.34   lotto 0.51  (no signal)
    features = [c for c in params.columns if c not in ("session_id", "n_points", *EXCLUDED_FEATURES)]
    run_cv(MODEL_NAME, make_fit_predict(params, features), MODEL_DIR, params={**PARAMS, "features": features})
    (out / "features.json").write_text(json.dumps(features, indent=2))
    table = params.set_index("session_id")
    save_final_model(MODEL_NAME, fit(final_training_frame(), table, features),
                     lambda fitted, test: predict(fitted, test, table), MODEL_DIR)

    # Descriptive: does each Stage-1 parameter differ between the groups?
    labels = load_table("sessions").set_index("session_id")["label"]
    p = params[fitted].set_index("session_id").join(labels)
    print("\nStage-1 parameters, median natural vs cheating")
    for c in features:
        m = p.groupby("label")[c].median()
        print(f"  {c:28s} {m.get(0, np.nan):9.3f}  {m.get(1, np.nan):9.3f}")


if __name__ == "__main__":
    main()
