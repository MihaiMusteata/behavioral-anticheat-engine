"""ml-service - HTTP wrapper around the detection engine (ml/inference).

The .NET Worker sends the sessions to score after they are submitted; this
service runs the trained voters + the voting ensemble and answers with every
model's probability and the final score, per session and per question. It is
stateless: models are loaded once at startup, nothing is stored here.

    POST /v1/detect   {"sessions": [<payload>, ...]}  ->  {"results": [...], ...}
    GET  /health      readiness + which voters are loaded

Payload / result formats: ml/inference/README.md (inference/features.py and
inference/engine.py docstrings).

Configuration (environment):
    ML_SKIP_VOTERS     voters to leave out, "level:voter[,voter];level:..."
                       default "question:02_tabpfn" - TabPFN at question level
                       costs ~33 s per call on CPU and ~6 GB RAM for a full exam;
                       the session-level ensemble keeps all 5 voters.
    ML_MAX_SESSIONS    max sessions per request (default 50)

Run locally:  ml/.venv/bin/uvicorn service.app:app --app-dir ml --port 8000
"""

from __future__ import annotations

import logging
import os
import sys
import threading
import time
from pathlib import Path

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

ML_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ML_DIR))
from inference.engine import DetectionEngine  # noqa: E402

log = logging.getLogger("ml-service")
logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")

MAX_SESSIONS = int(os.environ.get("ML_MAX_SESSIONS", "50"))


def parse_skip(value: str) -> dict[str, list[str]]:
    """"question:02_tabpfn;session:04_minirocket" -> {"question": [...], ...}"""
    skip: dict[str, list[str]] = {}
    for part in filter(None, (p.strip() for p in value.split(";"))):
        level, _, voters = part.partition(":")
        skip[level.strip()] = [v.strip() for v in voters.split(",") if v.strip()]
    return skip


SKIP = parse_skip(os.environ.get("ML_SKIP_VOTERS", "question:02_tabpfn"))

app = FastAPI(title="Behavioral Anticheat - ml-service", version="1.0.0")
_engine: DetectionEngine | None = None
_lock = threading.Lock()   # the models are not designed for concurrent predict()


class DetectRequest(BaseModel):
    sessions: list[dict] = Field(..., min_length=1)


@app.on_event("startup")
def load_engine() -> None:
    global _engine
    started = time.time()
    _engine = DetectionEngine(skip_voters=SKIP)
    log.info("detection engine loaded in %.1fs - session voters %s, question voters %s, skipped %s",
             time.time() - started, list(_engine.session_voters), list(_engine.question_voters), SKIP)


@app.get("/health")
def health() -> dict:
    if _engine is None:
        raise HTTPException(status_code=503, detail="engine not loaded")
    return {
        "status": "ok",
        "sessionVoters": list(_engine.config["session"]["voters"]),
        "questionVoters": list(_engine.config["question"]["voters"]),
        "skippedVoters": SKIP,
        "sessionMethod": _engine.config["session"]["method"],
    }


@app.post("/v1/detect")
def detect(request: DetectRequest) -> dict:
    if _engine is None:
        raise HTTPException(status_code=503, detail="engine not loaded")
    if len(request.sessions) > MAX_SESSIONS:
        raise HTTPException(status_code=413, detail=f"at most {MAX_SESSIONS} sessions per request")
    missing = [i for i, s in enumerate(request.sessions) if not s.get("sessionId")]
    if missing:
        raise HTTPException(status_code=422, detail=f"sessionId missing at positions {missing}")

    started = time.time()
    with _lock:
        try:
            results = _engine.predict(request.sessions)
        except Exception as exc:   # surfaced to the Worker, which marks the job failed and retries later
            log.exception("prediction failed for %d sessions", len(request.sessions))
            raise HTTPException(status_code=500, detail=f"prediction failed: {exc}") from exc
    elapsed = time.time() - started
    log.info("scored %d sessions in %.1fs", len(results), elapsed)
    return {
        "results": results,
        "elapsedSeconds": round(elapsed, 2),
        "sessionMethod": _engine.config["session"]["method"],
        "skippedVoters": SKIP,
    }
