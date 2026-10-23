## License

Source code: all rights reserved — see [`LICENSE`](LICENSE).
Dataset files (`datasets/`): CC BY 4.0 (open, attribution required) — see [`datasets/LICENSE`](datasets/LICENSE).

# Servicii Docker

| Serviciu | Ce face | Link acces |
| --- | --- | --- |
| **traefik** | Punct unic de intrare public — rutează `/api/*` către API, restul către client (SPA). Singurul port care trebuie tunelat cu ngrok pentru testul live. | http://localhost:8000 |
| **client** | Frontend React + TypeScript + Tailwind (Vite dev server) — interfața de examinare și dashboard-ul de proctoring. | http://localhost:5173 |
| **api** | Backend ASP.NET Core — autentificare, gestionare teste/examene, ingestion evenimente comportamentale, dashboard live. | http://localhost:8181 |
| **worker** | Serviciu de fundal — consumă evenimentele comportamentale din Kafka/Redpanda, calculează scorul de risc, persistă în Postgres. | — (fără port expus) |
| **postgres** | Baza de date relațională — utilizatori, teste, examene, evenimente comportamentale. | localhost:5432 |
| **redis** | Cache/store rapid — secrete de sesiune, nonce, secvențe, rate limiting, bus pentru dashboard-ul live. | localhost:6379 |
| **redpanda** | Broker Kafka-compatible — transportă evenimentele comportamentale de la API la Worker. | localhost:19092 |
| **redpanda-console** | UI web pentru Redpanda — inspectare topicuri, mesaje, consumer groups. | http://localhost:8081 |
| **otel-lgtm** | Stack observability all-in-one (Grafana + Prometheus + Loki + Tempo + Pyroscope + OTel Collector) — logs, metrici, traces din toate serviciile. | http://localhost:3000 (Grafana) |
| **postgres-exporter** | Expune metrici Postgres (conexiuni, tranzacții, dimensiune) pentru Prometheus. | — (fără port expus local) |
| **redis-exporter** | Expune metrici Redis (memorie, clienți conectați, comenzi/sec) pentru Prometheus. | — (fără port expus local) |
| **cadvisor** | Expune metrici de resurse per-container (CPU, memorie, rețea). | — (fără port expus local) |
| **kminion** | Expune lag-ul consumer group-ului Kafka (cât de în urmă e Worker-ul față de evenimentele publicate). | — (fără port expus local) |

## Date comportamentale colectate

### 1. Eveniment brut — `BehavioralEventRecord` (audit, append-only)

Pentru fiecare semnal trimis de client se stochează:

- `SessionId`, `ExamScheduleId`, `StudentId`, `QuestionId` — contextul evenimentului
- `Seq` — numărul de secvență (detectează lacune / reordonări / manipulare)
- `TimestampUtc` (client) vs `ReceivedAtUtc` (server) — detectează desincronizare de ceas sau replay
- `Nonce` + `Signature` — protecție anti-tamper / anti-replay
- `EventType` — tipul semnalului comportamental (vezi tabel mai jos)
- `PayloadJson` — detalii specifice evenimentului
- `ValidationFlags` — flaguri de validare (semnătură invalidă, gap de secvență etc.)

### 2. Tipuri de evenimente (`EventType`) capturate din browser

| Eveniment | Payload | Ce detectează |
|---|---|---|
| `window_blur` / `window_focus` | `activeElement`, `blurDurationMs` | schimbare de fereastră / tab |
| `visibility_change` | `visibilityState`, `hidden` | tab minimizat / ascuns |
| `copy` / `cut` / `paste` | `textLength` | copiere / lipire conținut |
| `fullscreen_enter` / `fullscreen_exit` | — | ieșire din modul fullscreen |
| `window_resize` | `width`, `height`, deltas | redimensionare suspectă (ex. deschidere DevTools) |
| `right_click` | `x`, `y` | click dreapta (meniu contextual) |
| `keyboard_shortcut` | `shortcut` | Ctrl+C/V/X/P/F/A, Ctrl+Shift+I/J, F12 |
| `print_attempt` | `source` | tentativă de printare |
| `idle_detected` | `idleMs` | inactivitate prelungită |
| `devtools_heuristic_triggered` | `widthGap`, `heightGap` | deschiderea DevTools (euristică outer/inner window) |
| `multiple_tabs_detected` | `detector` | sesiune deschisă în mai multe tab-uri (BroadcastChannel) |
| `session_resume` / `session_end` / `page_reload` / `page_unload` | `reason`, `offlineDurationMs`, `persisted` | reconectare, reload, închidere pagină, revenire din bfcache |
| `text_selected` | `zone` (prompt/opțiune), `optionId`, `selectionLength`, `startOffset`/`endOffset` | ce parte a enunțului/opțiunii a fost selectată — **nu** textul propriu-zis |
| `state_heartbeat` (la 8s) | `idleMs`, `windowGapWidth`/`Height`, `mouseX`/`Y`, `scrollY` | sampling continuu de stare (poziție cursor, scroll, inactivitate) |

### 3. Agregat per sesiune — `SessionFeatureAggregate`

- `TotalEvents`, `FocusLostCount`, `VisibilityHiddenCount`, `CopyCount`, `PasteCount`, `FullscreenExitCount`, `DevtoolsSignals`, `InactivitySignals`, `SequenceGapWarnings`
- `FeatureCountsJson` — dicționar generic `{feature_key: count}` pentru toate tipurile de eveniment, inclusiv cele fără contor dedicat
- `RiskScore` — scorul de risc curent al sesiunii (0–100)

### 4. Reguli de scoring — `ScoringRule` (ponderi configurabile în `appsettings.json → RiskScoring:Rules`)

| Feature key | Pondere |
|---|---|
| `window_blur_count` | 3 |
| `visibility_hidden_count` | 5 |
| `copy_count` | 4 |
| `cut_count` | 4 |
| `paste_count` | 7 |
| `fullscreen_exit_count` | 10 |
| `window_resize_count` | 3 |
| `right_click_count` | 2 |
| `keyboard_shortcut_count` | 6 |
| `text_burst_detected_count` | 12 |
| `devtools_heuristic_triggered_count` | 18 |
| `print_attempt_count` | 10 |
| `idle_detected_count` | 2 |
| `multiple_tabs_detected_count` | 14 |
| `page_reload_count` | 4 |
| `page_unload_count` | 4 |
| `event_validation_failed_count` | 15 |
| `event_sequence_gap_count` | 8 |
| `event_silence_detected_count` | 8 |

### 5. Istoric scor — `RiskScoreSnapshot`

`Score` (0–100) + `Reason`, cu timestamp la fiecare recalculare — permite reconstituirea evoluției riscului în timp pentru o sesiune.

### 6. Nivel sesiune de examen — `ExamSession`

- `CurrentRiskScore`, `SequenceGapWarnings`
- `ConsentAcceptedAtUtc` — acordul de monitorizare
- `SelfReportedCheated` + `SelfReportedAtUtc` — auto-raportare onestă a studentului
- `StartedAtUtc`, `EndsAtUtc`, `SubmittedAtUtc`, `LastActivityAtUtc` — timestamps ciclu de viață
