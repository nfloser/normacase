# 0002 – Modularer Monolith mit Spring Modulith

Status: angenommen · 21.11.2026

## Kontext
Die Fachlichkeit hat klar trennbare Bereiche (Eingang, Regelwerk, Bewertung, Bearbeitung, Rückgabe, Nachweis). Teams und Last rechtfertigen keine verteilten Dienste.

## Entscheidung
Ein Deployable mit Modulen als Java-Pakete. Spring Modulith verifiziert Grenzen und Zyklenfreiheit im Test; Module kommunizieren über ihre API oder Anwendungsereignisse. Jedes Modul hat ein eigenes Datenbankschema und eine eigene DB-Rolle.

## Folgen
- Einfacher Betrieb, eine Transaktion wo nötig.
- Ein späteres Herauslösen (etwa der Bewertung) ist möglich, weil Grenzen schon erzwungen sind.
- Verstöße fallen im CI auf, nicht erst im Review.
