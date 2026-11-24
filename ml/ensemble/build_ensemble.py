"""Build and evaluate the voting system from the voters' out-of-fold
predictions, then write the configuration the live engine uses.

Session level - 5 voters: 01 CatBoost, 02 TabPFN, 03 logistic regression,
04 MiniRocket, 05 Isolation Forest.
Question level - 4 voters (MiniRocket has no question variant: a question
spans 1-4 heartbeats, too short for a time series).

Methods compared (all on the same folds as the voters, no leakage):
  soft_weighted  FINAL (pre-declared): weighted mean of probabilities,
                 weight = ROC-AUC - 0.5 of each voter. Cross-fitted: the
                 weights used to score fold f come only from the OOF
                 predictions of the OTHER folds.
  soft_mean      plain mean of probabilities
  hard_majority  share of voters saying "cheating" at 0.5
  stacking       logistic regression on the voters' probabilities,
                 cross-fitted like the weights (risk: overfits on 310 rows)
plus a leave-one-voter-out run of soft_weighted (what each voter adds).

Question level: majority vote of 3 voters decides (2 of 3), the plain mean of
their probabilities is the score (no reliable question labels to fit weights
on). Evaluated: soft_mean (score) and hard_majority (share of suspect votes).

Thresholds: 0.5 ("suspect") and a strict one = 95th percentile of the
ensemble score on natural sessions (target <= 5% false accusations; the
reported recall/FPR at it are cross-fitted too).

Outputs:
  ml/ensemble/results/   *_metrics_*.json, ensemble_config.json   (tracked)
  ml/ensemble/artifacts/ ensemble OOF predictions                 (gitignored)

Run from the repo root (after every voter has been trained):
    ml/.venv/bin/python ml/ensemble/build_ensemble.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import roc_auc_score, roc_curve

ML_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ML_DIR))
from ensemble.voting import auc_weights, hard_vote, soft_vote  # noqa: E402
from evaluation.evaluate import (aggregate_to_sessions, balanced_weights, compute_metrics,  # noqa: E402
                                 print_summary)

ENSEMBLE_DIR = ML_DIR / "ensemble"
RESULTS = ENSEMBLE_DIR / "results"
ARTIFACTS = ENSEMBLE_DIR / "artifacts"

SESSION_VOTERS = ["01_catboost", "02_tabpfn", "03_logistic_regression", "04_minirocket", "05_isolation_forest"]
# 3 voters at question level - an odd number, so the majority vote never ties
# (2 of 3). TabPFN is left out here: ~33 s per call and GBs of RAM at 4,258
# context rows on CPU (it keeps its vote at session level).
QUESTION_VOTERS = ["01_catboost", "03_logistic_regression", "05_isolation_forest"]
FINAL_SESSION_METHOD = "soft_weighted"
FINAL_QUESTION_METHOD = "majority"   # decision: 2 of 3 votes; score: mean probability
STRICT_FPR = 0.05
SCHEMES = ("logo", "lotto")


def load_oof(voters: list[str], scheme: str, level: str) -> pd.DataFrame:
    """Wide table: one row per session (or question), one column per voter."""
    prefix = "question_" if level == "question" else ""
    keys = ["session_id", "question_id"] if level == "question" else ["session_id"]
    frames = []
    for v in voters:
        path = ML_DIR / "models" / v / "artifacts" / f"{prefix}oof_{scheme}.csv"
        if not path.exists():
            sys.exit(f"missing {path} - train {v} first")
        d = pd.read_csv(path)
        frames.append(d.set_index(keys)["proba"].rename(v))
    meta = pd.read_csv(ML_DIR / "models" / voters[0] / "artifacts" / f"{prefix}oof_{scheme}.csv").set_index(keys)
    meta = meta.drop(columns=["proba"])
    return meta.join(pd.concat(frames, axis=1)).reset_index()


def _auc(y: pd.Series, p: pd.Series) -> float:
    ok = p.notna()
    return roc_auc_score(y[ok], p[ok]) if y[ok].nunique() == 2 else 0.5


def cross_fitted(wide: pd.DataFrame, voters: list[str], method: str) -> pd.Series:
    """Ensemble score per row; anything learned (weights, stacker) for fold f
    uses only the rows of the other folds."""
    probs = wide[voters]
    if method == "soft_mean":
        return soft_vote(probs, {v: 1.0 for v in voters})
    if method == "hard_majority":
        return hard_vote(probs)
    out = pd.Series(np.nan, index=wide.index)
    for fold in wide["fold"].unique():
        test = wide["fold"] == fold
        train = wide[~test]
        if method == "soft_weighted":
            w = auc_weights({v: _auc(train["label"], train[v]) for v in voters})
            out[test] = soft_vote(probs[test], w)
        elif method == "stacking":
            x = train[voters].fillna(0.5).to_numpy()
            clf = LogisticRegression().fit(x, train["label"], sample_weight=balanced_weights(train["label"].to_numpy()))
            out[test] = clf.predict_proba(probs[test].fillna(0.5).to_numpy())[:, 1]
    return out


def strict_threshold(scores: pd.Series, labels: pd.Series) -> float:
    natural = scores[(labels == 0) & scores.notna()]
    return float(np.quantile(natural, 1 - STRICT_FPR))


def strict_metrics(oof: pd.DataFrame) -> dict:
    """Recall / FPR at the strict threshold, threshold cross-fitted per fold."""
    hits = []
    for fold in oof["fold"].unique():
        test = oof["fold"] == fold
        t = strict_threshold(oof.loc[~test, "proba"], oof.loc[~test, "label"])
        hits.append(oof[test].assign(flag=oof.loc[test, "proba"] >= t))
    h = pd.concat(hits)
    return {"strict_target_fpr": STRICT_FPR,
            "recall@strict": float(h.loc[h["label"] == 1, "flag"].mean()),
            "fpr@strict": float(h.loc[h["label"] == 0, "flag"].mean())}


def evaluate(wide: pd.DataFrame, score: pd.Series) -> tuple[pd.DataFrame, dict]:
    keys = [c for c in ("session_id", "question_id", "question_index") if c in wide]
    oof = wide[keys + ["group_id", "test", "label", "fold"]].assign(proba=score.to_numpy())
    metrics = compute_metrics(oof)
    metrics.update(strict_metrics(oof))
    return oof, metrics


def save(name: str, scheme: str, oof: pd.DataFrame, metrics: dict, extra: dict) -> None:
    RESULTS.mkdir(parents=True, exist_ok=True)
    ARTIFACTS.mkdir(parents=True, exist_ok=True)
    oof.to_csv(ARTIFACTS / f"{name}_oof_{scheme}.csv", index=False)
    (RESULTS / f"{name}_metrics_{scheme}.json").write_text(
        json.dumps({"model": name, "scheme": scheme, **extra, **metrics}, indent=2, default=str))


def main() -> None:
    summary = {}
    for scheme in SCHEMES:
        wide = load_oof(SESSION_VOTERS, scheme, "session")
        for method in ("soft_weighted", "soft_mean", "hard_majority", "stacking"):
            oof, m = evaluate(wide, cross_fitted(wide, SESSION_VOTERS, method))
            save(f"session_{method}", scheme, oof, m, {"level": "session", "method": method, "voters": SESSION_VOTERS})
            summary[(scheme, method)] = m
            if method == FINAL_SESSION_METHOD:
                print_summary("ENSEMBLE session soft_weighted (final)", scheme, m)
                print(f"  strict threshold: recall {m['recall@strict']:.3f}, FPR {m['fpr@strict']:.3f}")
        for left_out in SESSION_VOTERS:
            members = [v for v in SESSION_VOTERS if v != left_out]
            oof, m = evaluate(wide, cross_fitted(wide, members, FINAL_SESSION_METHOD))
            save(f"session_without_{left_out}", scheme, oof, m,
                 {"level": "session", "method": FINAL_SESSION_METHOD, "voters": members, "left_out": left_out})
            summary[(scheme, f"without {left_out}")] = m

        qwide = load_oof(QUESTION_VOTERS, scheme, "question")
        for method in ("soft_mean", "hard_majority"):
            oof, m = evaluate(qwide, cross_fitted(qwide, QUESTION_VOTERS, method))
            agg = {how: compute_metrics(aggregate_to_sessions(oof, how)) for how in ("top3_mean", "max", "mean")}
            save(f"question_{method}", scheme, oof, m,
                 {"level": "question (weak labels)", "method": method, "voters": QUESTION_VOTERS,
                  "aggregated_to_session": agg})
            summary[(scheme, f"question {method}")] = m
            summary[(scheme, f"question {method} -> session top3")] = agg["top3_mean"]
            if method == "soft_mean":
                print_summary("ENSEMBLE question (3 voters, mean) -> session (top3_mean)", scheme, agg["top3_mean"])

    print("\n=== ROC-AUC overview (logo / lotto)")
    for name in dict.fromkeys(k[1] for k in summary):
        lo, lt = summary.get(("logo", name), {}), summary.get(("lotto", name), {})
        print(f"  {name:45s} {lo.get('overall', {}).get('roc_auc', float('nan')):.3f} / "
              f"{lt.get('overall', {}).get('roc_auc', float('nan')):.3f}")

    # ---- deployment configuration: everything learned from the full logo OOF
    wide = load_oof(SESSION_VOTERS, "logo", "session")
    weights = auc_weights({v: _auc(wide["label"], wide[v]) for v in SESSION_VOTERS})
    session_score = soft_vote(wide[SESSION_VOTERS], weights)
    qwide = load_oof(QUESTION_VOTERS, "logo", "question")
    q_weights = {v: 1 / len(QUESTION_VOTERS) for v in QUESTION_VOTERS}
    q_score = soft_vote(qwide[QUESTION_VOTERS], q_weights)
    config = {
        "session": {
            "method": FINAL_SESSION_METHOD, "voters": SESSION_VOTERS,
            "weights": {k: round(v, 6) for k, v in weights.items()},
            "threshold": 0.5,
            "strict_threshold": round(strict_threshold(session_score, wide["label"]), 6),
            "strict_target_fpr": STRICT_FPR,
        },
        "question": {
            "method": FINAL_QUESTION_METHOD, "voters": QUESTION_VOTERS,
            "weights": {k: round(v, 6) for k, v in q_weights.items()},
            "threshold": 0.5,
            # never below the decision threshold, so "high" always means suspect
            "strict_threshold": round(max(strict_threshold(q_score, qwide["label"]), 0.5), 6),
            "strict_target_fpr": STRICT_FPR,
            "decision": "majority of the voters that voted (2 of 3)",
            "session_aggregation": "top3_mean",
        },
        "abstention": "weights renormalised over the voters that returned a value",
        "prior_note": ("Voters were trained with balanced classes (50/50). Scores are 'cheating-pattern "
                       "likelihood at a 50% base rate', not calibrated to a real exam's cheating rate."),
        "built_from": "out-of-fold predictions, scheme logo (leave-one-group-out), 310 sessions",
    }
    RESULTS.mkdir(parents=True, exist_ok=True)
    (RESULTS / "ensemble_config.json").write_text(json.dumps(config, indent=2))
    print(f"\nensemble_config.json -> session weights {config['session']['weights']}")
    print(f"strict thresholds: session {config['session']['strict_threshold']:.3f}, "
          f"question {config['question']['strict_threshold']:.3f}")


if __name__ == "__main__":
    main()
