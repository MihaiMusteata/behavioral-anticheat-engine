"""Shared evaluation harness - every voter in ml/models/ is evaluated here, the
same way, so their numbers (and their out-of-fold predictions, which the
ensemble combines later) are directly comparable.

Why group-based cross-validation: the label is assigned per group (a whole
class was told "behave naturally" or "cheat"), and sessions from the same
group share the room, the test and the instructions. A random split would put
sessions of the same group on both sides, so a model could score high just by
recognising the group. Two schemes, both with no group ever on both sides:

    logo   leave-one-group-out - 13 folds, one exam group held out each time.
           A held-out group has a single label, so metrics are computed on the
           pooled out-of-fold predictions, not averaged per fold.
    lotto  leave-one-test-out  - 4 folds (POO / APA / PC / TPA). Harder: the
           model never saw the test it is scored on. Every fold has both labels.

A model plugs in as one function:

    def fit_predict(train: pd.DataFrame, test: pd.DataFrame) -> np.ndarray

`train` / `test` are rows of datasets/ml/sessions.csv - or, for the
question-level models, of datasets/ml/questions.csv (pass `frame=`); folds are
always formed on groups, so every question of a session stays with its
session (test has no `label`
column, so it cannot leak by accident). `train` carries a `sample_weight`
column that balances the two classes 50/50 - models MUST train with it
(or an equivalent class_weight="balanced"). Reason: holding out a cheating
group lowers the cheating share of the training fold and vice versa, so any
learned prior/intercept moves *against* the held-out label. Pooled across
folds this alone drives a constant predictor to ROC-AUC 0.0 under logo.
Balanced weights pin the prior at 0.5 in every
fold. Models needing other files
(heartbeats, typing samples, calibration) load them via `load_table()` and
filter by `session_id`. Return one value per test row: a probability of
cheating in [0, 1], or NaN where the model abstains (e.g. no calibration).

Usage from a model script (results go to the model's own folder):

    from evaluation.evaluate import run_cv, feature_columns
    run_cv("01_catboost", fit_predict, model_dir)            # sessions
    run_cv("01_catboost", fit_predict, model_dir, level="question",
           frame=load_table("questions"))                     # questions

    <model_dir>/results/   metrics_{scheme}.json, final_model.json  (aggregate - tracked)
    <model_dir>/artifacts/ oof_{scheme}.csv, model.joblib           (student-level - gitignored)
    question-level files carry a "question_" prefix.

CLI (always through the ml venv - see ml/requirements.txt):
    ml/.venv/bin/python ml/evaluation/evaluate.py --compare      # table of all saved models
"""

from __future__ import annotations

import argparse
import json
import sys
import warnings
from pathlib import Path
from typing import Callable

import joblib
import numpy as np
import pandas as pd
from sklearn.metrics import (average_precision_score, brier_score_loss, f1_score, precision_score,
                             recall_score, roc_auc_score, roc_curve)

REPO_ROOT = Path(__file__).resolve().parents[2]
ML_DIR = REPO_ROOT / "ml"
DATA_DIR = REPO_ROOT / "datasets" / "ml"
LEVEL_PREFIX = {"session": "", "question": "question_"}

SCHEMES = {"logo": "group_id", "lotto": "test"}
BOOTSTRAP_ROUNDS = 2000
SEED = 42

FitPredict = Callable[[pd.DataFrame, pd.DataFrame], np.ndarray]


# ----------------------------------------------------------------- data access

_cache: dict[str, pd.DataFrame] = {}


def load_table(name: str) -> pd.DataFrame:
    """sessions | questions | heartbeats | typing_samples | calibration | events_clean"""
    if name not in _cache:
        path = DATA_DIR / f"{name}.csv"
        if not path.exists():
            sys.exit(f"missing {path} - run ml/data/build_dataset.py first")
        _cache[name] = pd.read_csv(path, low_memory=False)
    return _cache[name]


def load_catalog() -> dict:
    return json.loads((DATA_DIR / "feature_catalog.json").read_text())


# Columns that only describe which test was taken. Tests differ a lot in their
# cheating share (POO 40/56 vs APA 15/76), so under `logo` these would let a
# model learn "this test => cheating" instead of behavior.
TEST_IDENTITY_COLUMNS = ["n_questions", "n_free_text_questions"]


# Feature groups left out by default:
#   outcome      test score - confounded with test difficulty, adds +0.001 AUC
#   calibration  join-screen typing baseline - ablation 2026-11-08: no voter
#                improves with it (delta ROC-AUC -0.005..+0.006, every 95% CI
#                contains 0). Columns stay in the dataset for future work.
DEFAULT_EXCLUDED_GROUPS = ("outcome", "calibration")


def feature_columns(groups: list[str] | None = None, exclude: list[str] | None = None,
                    allow_test_identity: bool = False) -> list[str]:
    """Session feature columns by catalog group. Default: every group except
    DEFAULT_EXCLUDED_GROUPS, and without TEST_IDENTITY_COLUMNS."""
    fg = load_catalog()["feature_groups"]
    groups = groups or [g for g in fg if g not in DEFAULT_EXCLUDED_GROUPS]
    drop = set(exclude or []) | (set() if allow_test_identity else set(TEST_IDENTITY_COLUMNS))
    return [c for g in groups for c in fg[g] if c not in drop]


def signed_log1p(x: np.ndarray) -> np.ndarray:
    """Shared by several voters. Lives here (not in a model script) so pickled
    pipelines reference an importable module, not the script's __main__."""
    return np.sign(x) * np.log1p(np.abs(x))


# --------------------------------------------------------------- final model

def final_training_frame(frame: pd.DataFrame | None = None) -> pd.DataFrame:
    """All rows (sessions by default), with the same balanced `sample_weight`
    the CV folds get."""
    frame = (load_table("sessions") if frame is None else frame).copy()
    frame["sample_weight"] = balanced_weights(frame["label"].to_numpy())
    return frame


def model_paths(model_dir: Path, level: str = "session") -> tuple[Path, Path, str]:
    results, artifacts = model_dir / "results", model_dir / "artifacts"
    results.mkdir(parents=True, exist_ok=True)
    artifacts.mkdir(parents=True, exist_ok=True)
    return results, artifacts, LEVEL_PREFIX[level]


def save_final_model(model_name: str, fitted, predict: Callable, model_dir: Path, level: str = "session",
                     frame: pd.DataFrame | None = None, extra: dict | None = None) -> Path:
    """Persist a final model trained on all rows with the SAME fit() that was
    cross-validated. Load with joblib from a process that has ml/ on sys.path."""
    results, artifacts, prefix = model_paths(model_dir, level)
    path = artifacts / f"{prefix}model.joblib"
    joblib.dump(fitted, path)

    rows = (load_table("sessions") if frame is None else frame).drop(columns=["label"])
    info = {"model": model_name, "level": level, "trained_on_rows": int(len(rows)),
            "trained_on_sessions": int(rows["session_id"].nunique()),
            "file": f"artifacts/{path.name}", **(extra or {})}
    (results / f"{prefix}final_model.json").write_text(json.dumps(info, indent=2))
    print(f"final model ({level}): trained on {len(rows)} rows -> {path.relative_to(REPO_ROOT)} "
          f"({path.stat().st_size / 1e6:.1f} MB)")
    return path


# --------------------------------------------------------------------- folds

def balanced_weights(y: np.ndarray) -> np.ndarray:
    """Weights so both classes contribute equally (weighted prior = 0.5)."""
    counts = np.bincount(y.astype(int), minlength=2)
    return len(y) / (2 * counts[y.astype(int)])


def inner_group_splits(train: pd.DataFrame, n_splits: int = 4, seed: int = SEED) -> list[tuple[np.ndarray, np.ndarray]]:
    """Group-respecting splits *inside a training fold*, for hyperparameter
    selection. Groups of each label are shuffled and dealt round-robin, so
    every inner validation fold holds both labels (StratifiedGroupKFold does
    not guarantee that - it produced single-label folds on our 13 groups)."""
    rng = np.random.default_rng(seed)
    group_label = train.groupby("group_id")["label"].first()
    n_splits = min(n_splits, int(group_label.value_counts().min()))
    if n_splits < 2:
        raise ValueError("inner CV needs at least 2 groups of each label")
    assign = {}
    for label in (0, 1):
        groups = group_label[group_label == label].index.to_numpy()
        rng.shuffle(groups)
        for i, g in enumerate(groups):
            assign[g] = i % n_splits
    fold_of = train["group_id"].map(assign).to_numpy()
    return [(np.flatnonzero(fold_of != k), np.flatnonzero(fold_of == k)) for k in range(n_splits)]


def folds(sessions: pd.DataFrame, scheme: str):
    """Yield (fold_name, train_index, test_index). Deterministic order, so every
    model gets identical folds and the ensemble can align predictions."""
    key = SCHEMES[scheme]
    for value in sorted(sessions[key].unique()):
        test_mask = sessions[key].eq(value).to_numpy()
        yield str(value), np.flatnonzero(~test_mask), np.flatnonzero(test_mask)


def cross_validate(fit_predict: FitPredict, scheme: str, frame: pd.DataFrame | None = None) -> pd.DataFrame:
    sessions = load_table("sessions") if frame is None else frame
    keys = [c for c in ("session_id", "question_id", "question_index") if c in sessions.columns]
    rows = []
    for fold, tr, te in folds(sessions, scheme):
        train = sessions.iloc[tr].reset_index(drop=True)
        train["sample_weight"] = balanced_weights(train["label"].to_numpy())
        test = sessions.iloc[te].drop(columns=["label"]).reset_index(drop=True)
        proba = np.asarray(fit_predict(train, test), dtype=float).ravel()
        if len(proba) != len(test):
            raise ValueError(f"fold {fold}: model returned {len(proba)} values for {len(test)} rows")
        rows.append(pd.DataFrame({
            **{k: test[k] for k in keys}, "group_id": test["group_id"], "test": test["test"],
            "label": sessions.iloc[te]["label"].to_numpy(), "fold": fold, "proba": proba,
        }))
    oof = pd.concat(rows, ignore_index=True)
    valid = oof["proba"].dropna()
    if len(valid) and (valid.min() < 0 or valid.max() > 1):
        warnings.warn("predictions outside [0, 1] - calibrate scores to probabilities for soft voting")
    return oof


# ------------------------------------------------------------------- metrics

def _point_metrics(y: np.ndarray, p: np.ndarray) -> dict:
    out = {"n": int(len(y)), "positives": int(y.sum())}
    if len(np.unique(y)) < 2:
        return out
    out["roc_auc"] = float(roc_auc_score(y, p))
    out["pr_auc"] = float(average_precision_score(y, p))
    if p.min() >= 0 and p.max() <= 1:
        out["brier"] = float(brier_score_loss(y, p))
        pred = (p >= 0.5).astype(int)
        out["accuracy@0.5"] = float((pred == y).mean())
        out["precision@0.5"] = float(precision_score(y, pred, zero_division=0))
        out["recall@0.5"] = float(recall_score(y, pred, zero_division=0))
        out["f1@0.5"] = float(f1_score(y, pred, zero_division=0))
        out["false_positive_rate@0.5"] = float(((pred == 1) & (y == 0)).sum() / max((y == 0).sum(), 1))
    # Proctoring cost asymmetry: how many cheaters are caught while wrongly
    # flagging at most 5% / 10% of honest students. Descriptive (threshold
    # read off the pooled OOF curve), not a deployable operating point.
    fpr, tpr, _ = roc_curve(y, p)
    for cap in (0.05, 0.10):
        out[f"recall@fpr<={cap:.2f}"] = float(tpr[fpr <= cap].max())
    return out


def _group_bootstrap_ci(oof: pd.DataFrame, metric: str = "roc_auc") -> list[float] | None:
    """95% CI by resampling whole groups (sessions inside a group are not
    independent, so a per-session bootstrap would be over-confident)."""
    rng = np.random.default_rng(SEED)
    by_group = {g: d for g, d in oof.groupby("group_id")}
    names = list(by_group)
    values = []
    for _ in range(BOOTSTRAP_ROUNDS):
        sample = pd.concat([by_group[g] for g in rng.choice(names, size=len(names), replace=True)])
        y = sample["label"].to_numpy()
        if len(np.unique(y)) < 2:
            continue
        values.append(roc_auc_score(y, sample["proba"].to_numpy()) if metric == "roc_auc"
                      else average_precision_score(y, sample["proba"].to_numpy()))
    if len(values) < BOOTSTRAP_ROUNDS * 0.5:
        return None
    return [float(np.percentile(values, 2.5)), float(np.percentile(values, 97.5))]


def compute_metrics(oof: pd.DataFrame) -> dict:
    scored = oof.dropna(subset=["proba"])
    y, p = scored["label"].to_numpy(), scored["proba"].to_numpy()
    result = {
        "coverage": float(len(scored) / len(oof)),   # < 1 when the model abstains
        "overall": _point_metrics(y, p),
        "roc_auc_ci95_group_bootstrap": _group_bootstrap_ci(scored),
        "pr_auc_ci95_group_bootstrap": _group_bootstrap_ci(scored, "pr_auc"),
        "by_test": {},
    }
    # Folds holding both labels (all lotto folds) also get a per-fold AUC; its
    # mean cannot be distorted by offsets between folds.
    per_fold = {f: float(roc_auc_score(d["label"], d["proba"])) for f, d in scored.groupby("fold")
                if d["label"].nunique() == 2}
    if per_fold:
        result["roc_auc_per_fold"] = per_fold
        result["roc_auc_fold_mean"] = float(np.mean(list(per_fold.values())))
    for test, d in scored.groupby("test"):
        result["by_test"][test] = _point_metrics(d["label"].to_numpy(), d["proba"].to_numpy())
    return result


# ---------------------------------------------------------------- reporting

def save_results(model_name: str, model_dir: Path, scheme: str, oof: pd.DataFrame, metrics: dict,
                 params: dict | None, level: str = "session") -> None:
    results, artifacts, prefix = model_paths(model_dir, level)
    oof.to_csv(artifacts / f"{prefix}oof_{scheme}.csv", index=False)
    payload = {"model": model_name, "level": level, "scheme": scheme, "params": params or {}, **metrics}
    (results / f"{prefix}metrics_{scheme}.json").write_text(json.dumps(payload, indent=2, default=str))


def _fmt(v) -> str:
    return "  -  " if v is None or (isinstance(v, float) and np.isnan(v)) else f"{v:.3f}"


def print_summary(model_name: str, scheme: str, metrics: dict) -> None:
    o = metrics["overall"]
    ci = metrics["roc_auc_ci95_group_bootstrap"]
    ci_txt = f"[{ci[0]:.3f}, {ci[1]:.3f}]" if ci else "n/a"
    print(f"\n{model_name} - {scheme} (coverage {metrics['coverage']:.0%}, n={o['n']})")
    fold_mean = f"   fold-mean AUC {_fmt(metrics['roc_auc_fold_mean'])}" if "roc_auc_fold_mean" in metrics else ""
    print(f"  ROC-AUC {_fmt(o.get('roc_auc'))}  95% CI {ci_txt}   PR-AUC {_fmt(o.get('pr_auc'))}   "
          f"Brier {_fmt(o.get('brier'))}{fold_mean}")
    print(f"  @0.5: acc {_fmt(o.get('accuracy@0.5'))}  prec {_fmt(o.get('precision@0.5'))}  "
          f"rec {_fmt(o.get('recall@0.5'))}  F1 {_fmt(o.get('f1@0.5'))}  FPR {_fmt(o.get('false_positive_rate@0.5'))}")
    print(f"  recall at FPR<=5%: {_fmt(o.get('recall@fpr<=0.05'))}   at FPR<=10%: {_fmt(o.get('recall@fpr<=0.10'))}")
    print("  by test (ROC-AUC): " + "  ".join(f"{t} {_fmt(m.get('roc_auc'))}" for t, m in metrics["by_test"].items()))


def run_cv(model_name: str, fit_predict: FitPredict, model_dir: Path | None = None,
           schemes: tuple[str, ...] = ("logo", "lotto"), params: dict | None = None, save: bool = True,
           level: str = "session", frame: pd.DataFrame | None = None) -> dict[str, dict]:
    """Evaluate a voter under each scheme; save OOF predictions + metrics in
    model_dir (required when save=True)."""
    results = {}
    for scheme in schemes:
        oof = cross_validate(fit_predict, scheme, frame)
        metrics = compute_metrics(oof)
        if save:
            save_results(model_name, model_dir, scheme, oof, metrics, params, level)
        print_summary(f"{model_name} [{level}]", scheme, metrics)
        results[scheme] = metrics
    return results


# ------------------------------------------------------------ question level

def question_features() -> list[str]:
    return json.loads((DATA_DIR / "question_feature_catalog.json").read_text())["features"]


def aggregate_to_sessions(oof_q: pd.DataFrame, how: str) -> pd.DataFrame:
    """Question-level OOF -> one score per session. `top3_mean` (mean of the
    3 most suspicious questions) is the pre-declared primary rule: a single
    noisy question cannot flag a session, a few clearly suspicious ones can.
    Folds are formed on groups, so all questions of a session share a fold."""
    def top3(s: pd.Series) -> float:
        return s.nlargest(3).mean()
    agg = {"top3_mean": top3, "max": "max", "mean": "mean"}[how]
    scored = oof_q.dropna(subset=["proba"])
    out = scored.groupby("session_id").agg(group_id=("group_id", "first"), test=("test", "first"),
                                           label=("label", "first"), fold=("fold", "first"),
                                           proba=("proba", agg)).reset_index()
    return out


QUESTION_AGGREGATIONS = ("top3_mean", "max", "mean")


def run_question_cv(model_name: str, fit_predict: FitPredict, model_dir: Path, params: dict | None = None) -> dict:
    """Question-level CV + the same OOF aggregated back to sessions, so the
    question models can be compared with the session voters on the session
    labels (the question labels are weak - see build_question_dataset.py)."""
    questions = load_table("questions")
    out = {}
    for scheme in ("logo", "lotto"):
        oof = cross_validate(fit_predict, scheme, questions)
        metrics = compute_metrics(oof)
        save_results(model_name, model_dir, scheme, oof, metrics, params, level="question")
        print_summary(f"{model_name} [question, weak labels]", scheme, metrics)
        agg_metrics = {how: compute_metrics(aggregate_to_sessions(oof, how)) for how in QUESTION_AGGREGATIONS}
        results, _, _ = model_paths(model_dir, "question")
        (results / f"question_to_session_metrics_{scheme}.json").write_text(json.dumps(
            {"model": model_name, "level": "question->session", "scheme": scheme,
             "primary_aggregation": "top3_mean", "aggregations": agg_metrics}, indent=2, default=str))
        print_summary(f"{model_name} [question -> session, top3_mean]", scheme, agg_metrics["top3_mean"])
        out[scheme] = {"question": metrics, "session": agg_metrics}
    return out


def compare() -> None:
    files = sorted(ML_DIR.glob("**/results/*metrics_*.json"))
    files = [f for f in files if "archive" not in f.parts]
    if not files:
        print("no results yet under ml/**/results/")
        return
    rows = []
    for f in files:
        m = json.loads(f.read_text())
        if "overall" not in m:   # question->session files hold several aggregations
            continue
        o = m["overall"]
        ci = m.get("roc_auc_ci95_group_bootstrap")
        rows.append({"model": m["model"], "level": m.get("level", "session"), "scheme": m["scheme"],
                     "coverage": m["coverage"],
                     "roc_auc": o.get("roc_auc"), "auc_ci95": f"[{ci[0]:.2f}, {ci[1]:.2f}]" if ci else "n/a",
                     "fold_mean_auc": m.get("roc_auc_fold_mean"),
                     "pr_auc": o.get("pr_auc"), "f1@0.5": o.get("f1@0.5"),
                     "recall@fpr<=0.10": o.get("recall@fpr<=0.10"), "brier": o.get("brier")})
    table = pd.DataFrame(rows).sort_values(["level", "scheme", "roc_auc"], ascending=[False, True, False])
    with pd.option_context("display.float_format", "{:.3f}".format, "display.width", 180):
        for (level, scheme), d in table.groupby(["level", "scheme"], sort=False):
            print(f"\n== {level} / {scheme}")
            print(d.drop(columns=["level", "scheme"]).to_string(index=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--compare", action="store_true", help="compare all saved model results")
    args = parser.parse_args()
    if args.compare:
        compare()
    else:
        parser.print_help()
