"""Voter #4 - MiniRocket on the heartbeat time series (datasets/ml/heartbeats.csv).

The only voter that looks at *how a session evolves over time* rather than at
session totals. MiniRocket (Dempster et al., KDD 2021) convolves each series
with ~10,000 fixed, deterministic kernels and keeps, per kernel, the
proportion of positive values (PPV); a linear classifier on top does the
learning. State of the art for time-series classification at small scale,
and seconds to train - where LSTMs would overfit 310 series.

Input series per session (one ~8s heartbeat per step), multivariate:
  state     idle_ms, mouse_x, mouse_y, mouse_missing, scroll_y,
            window_gap_w, window_gap_h, question_index
  context   running ("so far") counts of 17 behavioral event types - leak-free,
            each step only sees events that happened before it

Preprocessing:
  - sessions are resampled to a fixed length (64 steps) by linear
    interpolation over relative time; absolute duration is already covered
    by the tabular voters;
  - missing mouse positions (touch devices) are forward/back-filled, the
    mouse_missing channel keeps the information;
  - sessions with fewer than 9 heartbeats (MiniRocket's kernel length) get
    no vote (NaN) - the ensemble decides from the other voters there.

Classifier: standardised MiniRocket features -> L2 logistic regression. C is
chosen by inner group-CV (both labels per inner fold) *inside the training fold only*, so the
outer evaluation stays honest. Trained with the harness's balanced weights.

Run from the repo root:
    ml/.venv/bin/python ml/models/04_minirocket/train.py
"""

from __future__ import annotations

import json
import sys
import warnings
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.linear_model import LogisticRegressionCV
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler
from sktime.transformations.panel.rocket import MiniRocketMultivariate

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, inner_group_splits, load_table, run_cv,  # noqa: E402
                                 save_final_model)

MODEL_NAME = "04_minirocket"
MODEL_DIR = Path(__file__).resolve().parent   # results/ (tracked) + artifacts/ (gitignored)

SERIES_LENGTH = 64
MIN_HEARTBEATS = 9

# Same order as build_dataset.RUNNING_COUNT_TYPES (the *_so_far columns).
RUNNING_COUNT_TYPES = ["window_blur", "visibility_change", "copy", "cut", "paste",
                       "text_burst_detected", "text_selected", "keyboard_shortcut", "right_click",
                       "devtools_heuristic_triggered", "window_resize", "idle_detected",
                       "multiple_tabs_detected", "page_reload", "answer_change", "answer_saved",
                       "question_navigation"]

STATE_CHANNELS = ["idle_ms", "mouse_x", "mouse_y", "mouse_missing", "scroll_y",
                  "window_gap_w", "window_gap_h", "question_index"]

PARAMS = {
    "series_length": SERIES_LENGTH,
    "min_heartbeats": MIN_HEARTBEATS,
    "num_kernels": 10_000,
    "Cs": np.logspace(-6, 0, 13).tolist(),
    "inner_folds": 4,
    "random_state": 42,
}


def channels() -> list[str]:
    return STATE_CHANNELS + [f"{t}_so_far" for t in RUNNING_COUNT_TYPES]


def build_panel(hb: pd.DataFrame | None = None) -> tuple[dict[str, np.ndarray], list[str]]:
    """session_id -> array (n_channels, SERIES_LENGTH), for sessions with
    enough heartbeats. `hb` defaults to the training table; ml/inference
    passes a live session's heartbeat rows (build_dataset.heartbeat_rows)."""
    hb = (load_table("heartbeats") if hb is None else hb).sort_values(["session_id", "hb_index"])
    cols = channels()
    target = np.linspace(0.0, 1.0, SERIES_LENGTH)
    panel = {}
    for sid, d in hb.groupby("session_id", sort=False):
        if len(d) < MIN_HEARTBEATS:
            continue
        d = d[cols].copy()
        d[["mouse_x", "mouse_y"]] = d[["mouse_x", "mouse_y"]].ffill().bfill().fillna(0)
        values = d.to_numpy(dtype=float)
        pos = np.linspace(0.0, 1.0, len(values))
        panel[sid] = np.stack([np.interp(target, pos, values[:, j]) for j in range(values.shape[1])])
    return panel, cols


_panel: dict[str, np.ndarray] | None = None


def get_panel() -> dict[str, np.ndarray]:
    global _panel
    if _panel is None:
        _panel = build_panel()[0]
    return _panel


def _stack(ids) -> np.ndarray:
    panel = get_panel()
    return np.stack([panel[i] for i in ids])


def fit(train: pd.DataFrame) -> dict:
    train = train[train["session_id"].isin(get_panel())].reset_index(drop=True)
    rocket = MiniRocketMultivariate(num_kernels=PARAMS["num_kernels"],
                                    random_state=PARAMS["random_state"], n_jobs=-1)
    x_train = rocket.fit_transform(_stack(train["session_id"])).to_numpy()
    clf = Pipeline([
        ("scale", StandardScaler()),
        ("clf", LogisticRegressionCV(Cs=PARAMS["Cs"], cv=inner_group_splits(train, PARAMS["inner_folds"]),
                                     scoring="roc_auc", max_iter=5000, n_jobs=-1)),
    ])
    clf.fit(x_train, train["label"], clf__sample_weight=train["sample_weight"].to_numpy())
    return {"rocket": rocket, "clf": clf}


def predict(fitted: dict, test: pd.DataFrame) -> np.ndarray:
    has = test["session_id"].isin(get_panel()).to_numpy()
    out = np.full(len(test), np.nan)
    if has.any():
        x_test = fitted["rocket"].transform(_stack(test.loc[has, "session_id"])).to_numpy()
        out[has] = fitted["clf"].predict_proba(x_test)[:, 1]
    return out


def fit_predict(train: pd.DataFrame, test: pd.DataFrame) -> np.ndarray:
    fitted = fit(train)
    chosen_C.append(float(fitted["clf"].named_steps["clf"].C_[0]))
    return predict(fitted, test)


chosen_C: list[float] = []


def main() -> None:
    warnings.filterwarnings("ignore", category=FutureWarning)
    panel, cols = build_panel()
    global _panel
    _panel = panel
    print(f"{MODEL_NAME}: {len(panel)} sessions with >= {MIN_HEARTBEATS} heartbeats, "
          f"{len(cols)} channels x {SERIES_LENGTH} steps")
    run_cv(MODEL_NAME, fit_predict, MODEL_DIR, params={**PARAMS, "channels": cols})
    print(f"\ninner-CV chosen C per outer fold: {sorted(set(chosen_C))}")
    (MODEL_DIR / "results" / "channels.json").write_text(json.dumps(cols, indent=2))
    save_final_model(MODEL_NAME, fit(final_training_frame()), predict, MODEL_DIR)


if __name__ == "__main__":
    main()
