# 0001 – Zielstack Angular, Spring Boot, PostgreSQL

Status: angenommen · 21.11.2026

## Kontext
Der bisherige Stand nutzt .NET und React. Für den Einsatzkontext ist der Stack Angular/TypeScript/RxJS, Java/Spring Boot und PostgreSQL vorgegeben.

## Entscheidung
Neuaufbau im selben Repository mit diesem Stack. Java 25 (LTS), Spring Boot 4.x, aktuelle Angular-Hauptversion, PostgreSQL 17. Der alte Stand bleibt unverändert unter `legacy/`.

## Folgen
- Eine Sprache je Schicht, keine Mischung mit .NET.
- Fachliche Erkenntnisse, Knowledge Packs und Fallakten werden übernommen; Code wird neu geschrieben.
- Die alte Implementierung dient als Prüforakel (ADR 0005).
