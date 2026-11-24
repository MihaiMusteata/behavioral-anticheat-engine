# ml-service: detection engine over HTTP (see ml/service/app.py).
# Build context = repository root:
#   docker build -f ml/service/Dockerfile -t bae-ml:local .
# Production (server is linux/amd64):
#   docker build --platform linux/amd64 -f ml/service/Dockerfile -t bae-ml:prod .
#
# The trained models (ml/models/*/artifacts/*.joblib) and the question norms
# (datasets/ml/question_norms.json) are gitignored - they are copied from the
# local working tree, so build from a machine where `ml/train_all.py` has run.
FROM python:3.11-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    OMP_NUM_THREADS=2 \
    MKL_NUM_THREADS=2 \
    NUMBA_NUM_THREADS=2 \
    LOKY_MAX_CPU_COUNT=2

WORKDIR /app

# libgomp: OpenMP runtime needed by catboost / torch / scikit-learn wheels.
RUN apt-get update && apt-get install -y --no-install-recommends libgomp1 && rm -rf /var/lib/apt/lists/*

# CPU-only PyTorch first (the default wheel bundles CUDA, several GB), same
# version as the training venv so the pickled TabPFN models load identically.
RUN pip install torch==2.15.0 --index-url https://download.pytorch.org/whl/cpu

COPY ml/requirements.txt ml/requirements.txt
RUN pip install -r ml/requirements.txt

COPY ml/ ml/
COPY datasets/ml/question_norms.json datasets/ml/question_norms.json

RUN useradd --create-home --uid 10001 mlservice && chown -R mlservice /app
USER mlservice

EXPOSE 8000
HEALTHCHECK --interval=30s --timeout=10s --start-period=120s --retries=3 \
    CMD python -c "import urllib.request,sys; sys.exit(0 if urllib.request.urlopen('http://127.0.0.1:8000/health', timeout=5).status == 200 else 1)"

CMD ["uvicorn", "service.app:app", "--app-dir", "ml", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
