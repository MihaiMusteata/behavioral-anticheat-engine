"""Voting rules - the single implementation used by the ensemble evaluation
(build_ensemble.py) and by the live engine (ml/inference). Pure functions of
the voters' probabilities; nothing here touches data files.

A voter may abstain (NaN, e.g. MiniRocket on a session with < 9 heartbeats):
weights are renormalised over the voters that did vote, so an abstention
never counts as a vote for either side.
"""

from __future__ import annotations

import numpy as np
import pandas as pd


def soft_vote(probs: pd.DataFrame, weights: dict[str, float]) -> pd.Series:
    """Weighted mean of the voters' probabilities (columns = voter names)."""
    w = pd.Series(weights, dtype=float).reindex(probs.columns).fillna(0.0)
    present = probs.notna()
    num = (probs.fillna(0.0) * w).sum(axis=1)
    den = (present * w).sum(axis=1)
    return num / den.replace(0, np.nan)


def hard_vote(probs: pd.DataFrame, threshold: float = 0.5) -> pd.Series:
    """Share of the voting models that say "cheating" (>= threshold).
    >= 0.5 means a majority of the models that voted."""
    votes = (probs >= threshold).astype(float).where(probs.notna())
    return votes.mean(axis=1)


def majority_votes(probs: pd.DataFrame, threshold: float = 0.5) -> tuple[pd.Series, pd.Series]:
    """(suspect votes, voters that voted) per row - abstentions don't count."""
    voted = probs.notna()
    suspect = ((probs >= threshold) & voted).sum(axis=1)
    return suspect, voted.sum(axis=1)


def decide_majority(suspect: int, voted: int, score: float, strict_threshold: float) -> dict:
    """Majority vote decides (2 of 3: an odd number of voters never ties);
    the mean probability only grades it. high = majority suspect AND mean at
    or above the strict threshold, medium = majority suspect, low = otherwise -
    so the level can never contradict the decision."""
    if voted == 0 or score is None or np.isnan(score):
        return {"decision": "insufficient_data", "level": "unknown"}
    if suspect * 2 > voted:
        return {"decision": "suspect", "level": "high" if score >= strict_threshold else "medium"}
    return {"decision": "not_suspect", "level": "low"}


def auc_weights(aucs: dict[str, float]) -> dict[str, float]:
    """Weight = how far a voter is above chance (ROC-AUC - 0.5), normalised.
    A voter at or below chance gets weight 0."""
    raw = {k: max(v - 0.5, 0.0) for k, v in aucs.items()}
    total = sum(raw.values())
    return {k: (v / total if total > 0 else 1 / len(raw)) for k, v in raw.items()}


def decide(score: float, threshold: float, strict_threshold: float | None = None) -> dict:
    """Score -> decision + suspicion level shown to the proctor."""
    if score is None or np.isnan(score):
        return {"decision": "insufficient_data", "level": "unknown"}
    if strict_threshold is not None and score >= strict_threshold:
        level = "high"
    elif score >= threshold:
        level = "medium"
    else:
        level = "low"
    return {"decision": "suspect" if score >= threshold else "not_suspect", "level": level}
