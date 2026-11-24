"""Rebuild both datasets, retrain the 5 voters (session + question level) and
the baseline, build the voting ensemble and write the evaluation report - one
command, always through the ml venv.
Archived voters (ml/archive/) are not part of it.

    ml/.venv/bin/python ml/train_all.py              # everything (~25 min, TabPFN dominates)
    ml/.venv/bin/python ml/train_all.py --skip-data  # keep datasets/ml as is
"""

from __future__ import annotations

import argparse
import subprocess
import sys
import time
from pathlib import Path

ML = Path(__file__).resolve().parent

STEPS = [
    ("dataset: sessions", ["data/build_dataset.py"]),
    ("dataset: questions", ["data/build_question_dataset.py"]),
    ("baseline: rules", ["baselines/rule_based_score/train.py"]),
    ("#1 CatBoost", ["models/01_catboost/train.py"]),
    ("#1 CatBoost [question]", ["models/01_catboost/train_question.py"]),
    ("#2 TabPFN", ["models/02_tabpfn/train.py"]),
    ("#2 TabPFN [question]", ["models/02_tabpfn/train_question.py"]),
    ("#3 logistic regression", ["models/03_logistic_regression/train.py"]),
    ("#3 logistic regression [question]", ["models/03_logistic_regression/train_question.py"]),
    ("#4 MiniRocket", ["models/04_minirocket/train.py"]),
    ("#5 Isolation Forest", ["models/05_isolation_forest/train.py"]),
    ("#5 Isolation Forest [question]", ["models/05_isolation_forest/train_question.py"]),
    ("ensemble", ["ensemble/build_ensemble.py"]),
    ("report", ["evaluation/report.py"]),
]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--skip-data", action="store_true")
    args = parser.parse_args()

    if Path(sys.prefix).resolve() != (ML / ".venv").resolve():
        sys.exit(f"run through the ml venv: {ML / '.venv/bin/python'} {Path(__file__).name}")

    for name, cmd in STEPS:
        if args.skip_data and name.startswith("dataset"):
            continue
        print(f"\n===== {name}", flush=True)
        start = time.time()
        subprocess.run([sys.executable, str(ML / cmd[0]), *cmd[1:]], check=True, cwd=ML.parent)
        print(f"----- {name}: {time.time() - start:.0f}s", flush=True)


if __name__ == "__main__":
    main()
