"""Run the detection engine from the command line.

    ml/.venv/bin/python ml/inference/cli.py --session-id <uuid>        # historical session from the dump
    ml/.venv/bin/python ml/inference/cli.py --payload session.json      # any payload (see features.py)
    ml/.venv/bin/python ml/inference/cli.py --session-id <uuid> --write-payload example.json

Prints the engine's JSON result (see engine.py for the format).
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))  # ml/
from inference.features import payload_from_dump  # noqa: E402


def main() -> None:
    parser = argparse.ArgumentParser()
    src = parser.add_mutually_exclusive_group(required=True)
    src.add_argument("--session-id", help="build the payload from datasets/db_*.csv")
    src.add_argument("--payload", type=Path, help="JSON payload file")
    parser.add_argument("--write-payload", type=Path, help="also save the payload built from the dump")
    args = parser.parse_args()

    payload = payload_from_dump(args.session_id) if args.session_id else json.loads(args.payload.read_text())
    if args.write_payload:
        args.write_payload.write_text(json.dumps(payload, indent=2, default=str))

    from inference.engine import DetectionEngine
    print(json.dumps(DetectionEngine().predict([payload])[0], indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
