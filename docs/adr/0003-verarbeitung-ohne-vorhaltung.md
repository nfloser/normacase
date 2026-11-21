# 0003 – Verarbeitung ohne Vorhaltung von Gesundheitsdaten

Status: angenommen · 21.11.2026

## Kontext
NormaCase ergänzt ein führendes Fachsystem. Eine zweite Ablage von Gesundheitsdaten würde Pflichten verdoppeln und das Risiko erhöhen.

## Entscheidung
Standard ist Durchreichen; mit Prüfwerkstatt werden Vorgänge befristet gehalten und nach Rückgabe gelöscht. Dauerhaft bleibt nur ein Entscheidungsnachweis ohne Fallinhalt (Hash, Versionen, Regel-IDs, Ergebnis). Einzelheiten und Kriterienzuordnung: `docs/03-datenschutz-und-datenhaltung.md`.

## Folgen
- Reproduzierbarkeit beruht auf erneutem Einreichen der Originaleingabe durch das Fachsystem.
- Jede neue Spalte braucht Zweck und Löschregel.
- Löschung und Inhaltsfreiheit werden automatisiert getestet.
