"""Question-level variant of this voter - TabPFN v2 on ONE question at a time.

Same classifier as the session voter (train.py), with two speed settings
that matter at question scale (~4,200 context rows instead of ~300):
  - n_estimators=1 (session voter: 4) - one fold drops from ~180 s to ~20 s,
    measured ROC-AUC unchanged on the TPA hold-out (0.763 either way);
  - the saved final model keeps the default fit mode. fit_with_cache was
    tried (encode the 4,258-row context once, then ~0.2 s per session) but the
    pickled model is 333 MB and fitting/reloading takes ~3 min on CPU.
    Default mode: 30 MB, ~33 s to score one session's questions on CPU
    (measured; the whole context is re-read per call, so batching sessions
    amortises it) - fine for scoring sessions after they are submitted, too
    slow for live per-event scoring; a GPU makes it near-instant, or the
    engine can skip this voter (DetectionEngine(skip_voters=...)).

Input:  one row of datasets/ml/questions.csv per (session, question).
Output: probability that the question belongs to a cheating pattern.
Labels are weak (session label on every question) - see run_question_cv.

Run from the repo root (~10 min on CPU):
    ml/.venv/bin/python ml/models/02_tabpfn/train_question.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
import warnings
from pathlib import Path

import pandas as pd
from tabpfn import TabPFNClassifier

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, load_table, question_features,  # noqa: E402
                                 run_question_cv, save_final_model)

MODEL_DIR = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("session_voter", MODEL_DIR / "train.py")
voter = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(voter)

MODEL_NAME = voter.MODEL_NAME

PARAMS = {**voter.PARAMS, "n_estimators": 1, "ignore_pretraining_limits": True}


def fit(train: pd.DataFrame, features: list[str], fit_mode: str = "fit_preprocessors") -> dict:
    x, y = train[features].to_numpy(dtype=float), train["label"].to_numpy()
    model = TabPFNClassifier(**PARAMS, fit_mode=fit_mode)
    model.fit(x, y)
    return {"model": model, "features": features, "x_train": x, "y_train": y, "params": PARAMS}


def main() -> None:
    warnings.filterwarnings("ignore", message=".*GPU.*")
    features = question_features()
    print(f"{MODEL_NAME} [question]: {len(features)} features")
    run_question_cv(MODEL_NAME, lambda train, test: voter.predict(fit(train, features), test), MODEL_DIR,
                    params={**PARAMS, "n_features": len(features)})
    (MODEL_DIR / "results" / "question_features.json").write_text(json.dumps(features, indent=2))
    questions = load_table("questions")
    save_final_model(MODEL_NAME, fit(final_training_frame(questions), features),
                     voter.predict, MODEL_DIR, level="question", frame=questions)


if __name__ == "__main__":
    main()
