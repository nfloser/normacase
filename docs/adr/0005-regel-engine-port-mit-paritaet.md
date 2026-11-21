# 0005 – Regel-Engine in Java mit Paritätsnachweis

Status: angenommen · 21.11.2026

## Kontext
Die .NET-Regel-Engine im Referenzstand ist getestet und liefert für Knowledge Packs und synthetische Fallakten bekannte Ergebnisse.

## Entscheidung
Die Engine wird in Java als framework-freie Bibliothek neu geschrieben. Aus `legacy/` werden Golden Files erzeugt (Eingabe, Release, Stichtag → Ergebnis-JSON). Die Java-Engine muss sie bitgleich reproduzieren; Abweichungen sind Fehler, bis sie fachlich begründet und dokumentiert sind. Dezimalwerte werden exakt (`BigDecimal`) verarbeitet.

## Folgen
- Der Neuaufbau beweist Gleichwertigkeit statt sie zu behaupten.
- Golden Files werden einmalig erzeugt und versioniert; `legacy/` selbst bleibt unverändert.
