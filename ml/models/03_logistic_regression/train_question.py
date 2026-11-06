"""Question-level variant of this voter - L2 logistic regression on ONE question at a time.

Same pipeline as the session voter (train.py: signed log1p, median
imputation + missing indicators, standardisation, C=0.1), on question rows.

Input:  one row of datasets/ml/questions.csv per (session, question) - the
        features of question_feature_catalog.json, computed from that
        question's events only (build_question_dataset.py).
Output: probability that the question belongs to a cheating pattern.

Labels are weak (session label copied to every question), so the evaluation
reports both question-level metrics and the same predictions aggregated back
to sessions (top-3 mean), see evaluation.evaluate.run_question_cv.

Run from the repo root:
    ml/.venv/bin/python ml/models/03_logistic_regression/train_question.py
"""

from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))  # ml/
from evaluation.evaluate import (final_training_frame, load_table, question_features,  # noqa: E402
                                 run_question_cv, save_final_model)

MODEL_DIR = Path(__file__).resolve().parent
_spec = importlib.util.spec_from_file_location("session_voter", MODEL_DIR / "train.py")
voter = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(voter)

MODEL_NAME = voter.MODEL_NAME


def main() -> None:
    features = question_features()
    print(f"{MODEL_NAME} [question]: {len(features)} features")
    run_question_cv(MODEL_NAME, lambda train, test: voter.predict(voter.fit(train, features), test), MODEL_DIR,
                    params={"n_features": len(features), "same_params_as": "train.py"})
    (MODEL_DIR / "results" / "question_features.json").write_text(json.dumps(features, indent=2))
    questions = load_table("questions")
    save_final_model(MODEL_NAME, voter.fit(final_training_frame(questions), features), voter.predict,
                     MODEL_DIR, level="question", frame=questions)


if __name__ == "__main__":
    main()
