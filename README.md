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
