"""Shared Postgres connection helper for the dataset export scripts."""

import os

import psycopg2


def get_connection():
    # Defaults match the local docker-compose setup (postgres exposed on
    # localhost:5432). Override via env vars when pointing at another
    # server, e.g. PGHOST=my-server PGPASSWORD=... python export_session_dataset.py
    return psycopg2.connect(
        host=os.environ.get("PGHOST", "localhost"),
        port=os.environ.get("PGPORT", "5432"),
        dbname=os.environ.get("PGDATABASE", "behavioral_anticheat"),
        user=os.environ.get("PGUSER", "postgres"),
        password=os.environ.get("PGPASSWORD", "postgres"),
    )
