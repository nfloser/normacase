# 0006 – Authentisierung über OIDC, Autorisierung je Fall

Status: angenommen · 21.11.2026

## Entscheidung
Anmeldung über OpenID Connect (lokal Keycloak, im Haus der vorhandene IdP). Die SPA nutzt Authorization Code mit PKCE, das Backend ist Resource Server. Rollen kommen aus dem IdP, Korbzuordnung und fallbezogene Rechte aus dem Modul `identity`. Fehlendes Recht und fehlender Fall ergeben dieselbe 404. Prüfung und Freigabe durch dieselbe Person sind technisch ausgeschlossen.

## Folgen
- Keine eigene Passwortverwaltung.
- Jede Abfrage mit Fallbezug braucht einen Rechtetest.
