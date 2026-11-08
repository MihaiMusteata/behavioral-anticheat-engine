"""Evaluation report: every saved model x every metric, each score with a
qualitative rating (foarte slab / slab / acceptabil / bun / foarte bun).

Reads every */results/*metrics_*.json under ml/ (voters, baseline, ensemble)
and writes:
  ml/evaluation/report.md        everything in one place (for the thesis)
  ml/<...>/results/metrics.md    one file per model, next to the model

Rating scales - stated explicitly so they can be cited / argued in the thesis:
  ROC-AUC     Hosmer & Lemeshow, Applied Logistic Regression (3rd ed., 2013):
              0.5 no discrimination, 0.7-0.8 acceptable, 0.8-0.9 excellent,
              >= 0.9 outstanding. Mapped to: <0.6 foarte slab, 0.6-0.7 slab,
              0.7-0.8 acceptabil, 0.8-0.9 bun, >=0.9 foarte bun.
  others      project conventions, anchored on chance level. Classes are
              balanced (155/155), so a coin flip scores 0.5 on accuracy /
              precision / recall / F1 / PR-AUC and 0.25 on Brier.

Run from the repo root:
    ml/.venv/bin/python ml/evaluation/report.py
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))  # ml/
from evaluation.evaluate import ML_DIR  # noqa: E402

LABELS = ["foarte slab", "slab", "acceptabil", "bun", "foarte bun"]

# metric -> (thresholds for slab, acceptabil, bun, foarte bun; higher_is_better)
SCALES = {
    "roc_auc":          ((0.60, 0.70, 0.80, 0.90), True),
    "pr_auc":           ((0.60, 0.70, 0.80, 0.90), True),
    "accuracy@0.5":     ((0.60, 0.70, 0.80, 0.90), True),
    "precision@0.5":    ((0.60, 0.70, 0.80, 0.90), True),
    "recall@0.5":       ((0.60, 0.70, 0.80, 0.90), True),
    "f1@0.5":           ((0.60, 0.70, 0.80, 0.90), True),
    "recall@fpr<=0.05": ((0.30, 0.50, 0.70, 0.85), True),
    "recall@fpr<=0.10": ((0.30, 0.50, 0.70, 0.85), True),
    "false_positive_rate@0.5": ((0.25, 0.15, 0.10, 0.05), False),
    "brier":            ((0.25, 0.20, 0.15, 0.10), False),
    "coverage":         ((0.60, 0.80, 0.95, 1.00), True),
}

COLUMNS = [
    ("coverage", "Acoperire"),
    ("roc_auc", "ROC-AUC"),
    ("pr_auc", "PR-AUC"),
    ("accuracy@0.5", "Acuratețe"),
    ("precision@0.5", "Precizie"),
    ("recall@0.5", "Recall"),
    ("f1@0.5", "F1"),
    ("false_positive_rate@0.5", "Acuzații false (FPR)"),
    ("recall@fpr<=0.05", "Prinși la ≤5% FPR"),
    ("recall@fpr<=0.10", "Prinși la ≤10% FPR"),
    ("brier", "Brier"),
]

EXPLAIN = {
    "Acoperire": "pentru câte sesiuni votează modelul (restul: se abține)",
    "ROC-AUC": "cât de bine ordonează trișorii deasupra celor cinstiți, indiferent de prag (0.5 = ghicit, 1 = perfect)",
    "PR-AUC": "ca ROC-AUC, dar concentrat pe clasa „cheating” (0.5 = ghicit)",
    "Acuratețe": "procentul de sesiuni clasificate corect la pragul 0.5",
    "Precizie": "din cei acuzați de model, câți chiar erau din grupa cheating",
    "Recall": "din trișori, câți au fost prinși la pragul 0.5",
    "F1": "media armonică dintre precizie și recall",
    "Acuzații false (FPR)": "din studenții cinstiți, câți au fost acuzați greșit la pragul 0.5 (mai mic = mai bine)",
    "Prinși la ≤5% FPR": "câți trișori prinde modelul dacă acuză pe nedrept cel mult 5% din cei cinstiți",
    "Prinși la ≤10% FPR": "la fel, cu toleranță de 10% acuzații false",
    "Brier": "eroarea medie a probabilităților (0 = perfect, 0.25 = ghicit; mai mic = mai bine)",
    "Prag strict": "prag ales astfel încât ≤5% din sesiunile natural să fie acuzate; recall/FPR măsurate cross-fitted",
}

VOTERS = [("01_catboost", "#1 CatBoost"), ("02_tabpfn", "#2 TabPFN"), ("03_logistic_regression", "#3 Regresie logistică"),
          ("04_minirocket", "#4 MiniRocket"), ("05_isolation_forest", "#5 Isolation Forest")]
ENSEMBLE_METHODS = [("soft_weighted", "Ansamblu: soft voting ponderat (FINAL)"), ("soft_mean", "Ansamblu: soft voting egal"),
                    ("hard_majority", "Ansamblu: vot majoritar (hard)"), ("stacking", "Ansamblu: stacking")]
SCHEME_TITLE = {
    "logo": "Validare pe grupe (leave-one-group-out, 13 fold-uri)",
    "lotto": "Validare pe teste nevăzute (leave-one-test-out, 4 fold-uri)",
}
MODELS_DIR, ENSEMBLE_RESULTS = ML_DIR / "models", ML_DIR / "ensemble" / "results"
BASELINE_RESULTS = ML_DIR / "baselines" / "rule_based_score" / "results"


def rate(metric: str, value: float | None) -> str:
    if value is None:
        return "-"
    thresholds, higher = SCALES[metric]
    level = sum(value >= t for t in thresholds) if higher else sum(value <= t for t in thresholds)
    return LABELS[level]


def cell(metric: str, value: float | None) -> str:
    if value is None:
        return "–"
    shown = f"{value:.0%}" if metric in ("coverage", "false_positive_rate@0.5") or metric.startswith("recall@fpr") \
        else f"{value:.3f}"
    return f"{shown} ({rate(metric, value)})"


def read(path: Path) -> dict | None:
    return json.loads(path.read_text()) if path.exists() else None


def table(entries: list[tuple[str, dict | None]]) -> list[str]:
    lines = ["| Model | " + " | ".join(t for _, t in COLUMNS) + " | ROC-AUC 95% CI |",
             "|---|" + "---|" * (len(COLUMNS) + 1)]
    for name, m in entries:
        if not m:
            continue
        o = {**m["overall"], "coverage": m["coverage"]}
        ci = m.get("roc_auc_ci95_group_bootstrap")
        lines.append(f"| {name} | " + " | ".join(cell(k, o.get(k)) for k, _ in COLUMNS)
                     + f" | {f'{ci[0]:.2f}–{ci[1]:.2f}' if ci else '–'} |")
    return lines + [""]


def by_test_table(entries: list[tuple[str, dict | None]]) -> list[str]:
    entries = [(n, m) for n, m in entries if m]
    tests = sorted({t for _, m in entries for t in m.get("by_test", {})})
    lines = ["| Model | " + " | ".join(tests) + " |", "|---|" + "---|" * len(tests)]
    for name, m in entries:
        bt = m.get("by_test", {})
        lines.append(f"| {name} | " + " | ".join(cell("roc_auc", bt.get(t, {}).get("roc_auc")) for t in tests) + " |")
    return lines + [""]


def scale_legend() -> list[str]:
    lines = ["| Metrică | foarte slab | slab | acceptabil | bun | foarte bun |", "|---|---|---|---|---|---|"]
    for key, title in COLUMNS:
        thresholds, higher = SCALES[key]
        pct = key in ("coverage", "false_positive_rate@0.5") or key.startswith("recall@fpr")
        fmt = (lambda v: f"{v:.0%}") if pct else (lambda v: f"{v:.2f}")
        t = [fmt(v) for v in thresholds]
        if key == "coverage":
            row = ["< 60%", "60–80%", "80–95%", "95–99%", "100%"]
        elif higher:
            row = [f"< {t[0]}", f"{t[0]}–{t[1]}", f"{t[1]}–{t[2]}", f"{t[2]}–{t[3]}", f"≥ {t[3]}"]
        else:
            row = [f"> {t[0]}", f"{t[1]}–{t[0]}", f"{t[2]}–{t[1]}", f"{t[3]}–{t[2]}", f"≤ {t[3]}"]
        lines.append(f"| {title} | " + " | ".join(row) + " |")
    return lines


def legend() -> list[str]:
    return (["## Ce înseamnă fiecare metrică", ""] + [f"- **{k}** - {v}" for k, v in EXPLAIN.items()]
            + ["", "## Scala de evaluare", "",
               "ROC-AUC: Hosmer & Lemeshow (2013). Celelalte: convenții ancorate pe nivelul de ghicit "
               "(clase echilibrate → 0.5; Brier → 0.25).", ""] + scale_legend())


def session_entries(scheme: str) -> list[tuple[str, dict | None]]:
    rows = [("Reguli actuale (referință)", read(BASELINE_RESULTS / f"metrics_{scheme}.json"))]
    rows += [(label, read(MODELS_DIR / key / "results" / f"metrics_{scheme}.json")) for key, label in VOTERS]
    rows += [(label, read(ENSEMBLE_RESULTS / f"session_{m}_metrics_{scheme}.json")) for m, label in ENSEMBLE_METHODS]
    return rows


def question_to_session_entries(scheme: str) -> list[tuple[str, dict | None]]:
    rows = []
    for key, label in VOTERS:
        m = read(MODELS_DIR / key / "results" / f"question_to_session_metrics_{scheme}.json")
        rows.append((f"{label} pe întrebări → sesiune", m["aggregations"]["top3_mean"] if m else None))
    e = read(ENSEMBLE_RESULTS / f"question_soft_mean_metrics_{scheme}.json")
    rows.append(("Ansamblu pe întrebări → sesiune (FINAL)", e["aggregated_to_session"]["top3_mean"] if e else None))
    return rows


def question_entries(scheme: str) -> list[tuple[str, dict | None]]:
    rows = [(label, read(MODELS_DIR / key / "results" / f"question_metrics_{scheme}.json")) for key, label in VOTERS]
    rows.append(("Ansamblu pe întrebări: soft voting egal (FINAL)",
                 read(ENSEMBLE_RESULTS / f"question_soft_mean_metrics_{scheme}.json")))
    return rows


def strict_lines(scheme: str) -> list[str]:
    lines = ["| Ansamblu | Prag strict: trișori prinși | Prag strict: acuzații false |", "|---|---|---|"]
    for m, label in ENSEMBLE_METHODS:
        d = read(ENSEMBLE_RESULTS / f"session_{m}_metrics_{scheme}.json")
        if d:
            lines.append(f"| {label} | {cell('recall@fpr<=0.05', d['recall@strict'])} | "
                         f"{cell('false_positive_rate@0.5', d['fpr@strict'])} |")
    return lines + [""]


def leave_one_out(scheme: str) -> list[str]:
    final = read(ENSEMBLE_RESULTS / f"session_soft_weighted_metrics_{scheme}.json")
    if not final:
        return []
    base = final["overall"]["roc_auc"]
    lines = [f"Ansamblul final: ROC-AUC {base:.3f}. Fără fiecare votant:", "",
             "| Votant scos | ROC-AUC fără el | Diferență |", "|---|---|---|"]
    for key, label in VOTERS:
        d = read(ENSEMBLE_RESULTS / f"session_without_{key}_metrics_{scheme}.json")
        if d:
            auc = d["overall"]["roc_auc"]
            lines.append(f"| {label} | {auc:.3f} | {auc - base:+.3f} |")
    return lines + [""]


def build_global() -> str:
    out = ["# Raport de evaluare - sistemul de detecție (5 votanți + ansamblu)", "",
           "Toate scorurile sunt calculate pe date pe care modelul **nu le-a văzut la antrenare** "
           "(predicții out-of-fold). 310 sesiuni (155 natural / 155 cheating), 13 grupe, 4 teste; "
           "4.258 de întrebări. Generat de `ml/evaluation/report.py`.", ""]
    out += ["# 1. Nivel sesiune", ""]
    for scheme in ("logo", "lotto"):
        out += [f"## {SCHEME_TITLE[scheme]}", ""] + table(session_entries(scheme))
        out += ["Pragul strict al ansamblului (țintă: ≤5% acuzații false):", ""] + strict_lines(scheme)
    out += ["## Contribuția fiecărui votant (ansamblul final fără el, validare pe grupe)", ""] + leave_one_out("logo")
    out += ["## ROC-AUC pe fiecare test (validare pe grupe)", ""] + by_test_table(session_entries("logo"))
    out += ["# 2. Nivel întrebare", "",
            "Etichetele pe întrebare sunt **slabe** (eticheta sesiunii copiată pe fiecare întrebare): "
            "în sesiunile cheating există și întrebări rezolvate cinstit, deci metricile de mai jos "
            "sunt o limită inferioară. Comparația corectă cu nivelul sesiune e în secțiunea 2.2.", ""]
    for scheme in ("logo", "lotto"):
        out += [f"## 2.1 Pe întrebare - {SCHEME_TITLE[scheme]}", ""] + table(question_entries(scheme))
    for scheme in ("logo", "lotto"):
        out += [f"## 2.2 Întrebări agregate pe sesiune (media celor mai suspecte 3) - {SCHEME_TITLE[scheme]}", ""]
        out += table(question_to_session_entries(scheme))
    return "\n".join(out + legend()) + "\n"


def build_model(key: str, label: str, results: Path) -> str | None:
    out = [f"# Metrici - {label}", "", "Generat de `ml/evaluation/report.py` din `results/*.json`. "
           "Scorurile sunt out-of-fold (date nevăzute la antrenare).", ""]
    found = False
    for scheme in ("logo", "lotto"):
        m = read(results / f"metrics_{scheme}.json")
        if m:
            found = True
            out += [f"## Sesiune - {SCHEME_TITLE[scheme]}", ""] + table([(label, m)]) + by_test_table([(label, m)])
    for scheme in ("logo", "lotto"):
        q = read(results / f"question_metrics_{scheme}.json")
        a = read(results / f"question_to_session_metrics_{scheme}.json")
        if q:
            found = True
            out += [f"## Întrebare (etichete slabe) - {SCHEME_TITLE[scheme]}", ""] + table([(label, q)])
            out += [f"## Întrebări → sesiune (agregare) - {SCHEME_TITLE[scheme]}", ""]
            out += table([(f"{label}, {how}", a["aggregations"][how]) for how in ("top3_mean", "max", "mean")])
    return "\n".join(out + legend()) + "\n" if found else None


def main() -> None:
    report = build_global()
    path = ML_DIR / "evaluation" / "report.md"
    path.write_text(report)
    written = [path]
    targets = [("00_rule_based_score", "Reguli actuale (referință)", BASELINE_RESULTS)] + \
              [(k, l, MODELS_DIR / k / "results") for k, l in VOTERS]
    for key, label, results in targets:
        text = build_model(key, label, results)
        if text:
            (results / "metrics.md").write_text(text)
            written.append(results / "metrics.md")
    print(report)
    print("written:", *[str(p.relative_to(ML_DIR.parent)) for p in written], sep="\n  ")


if __name__ == "__main__":
    main()
