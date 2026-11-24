# Keycloak-Testrealm

`normacase-realm.json` wird beim Start von `docker compose up` importiert. Alle Konten sind synthetisch und nur für die lokale Vorführung gedacht. Passwort jeweils `demo`.

| Benutzer | Rolle |
|---|---|
| `gutachterin.a` (Anna Seidel), `gutachter.b` (Bernd Krause) | gutachter |
| `freigabe.c` (Clara Wendt) | freigabe |
| `redaktion.d` | regelwerk-redaktion |
| `regelwerk.e` | regelwerk-freigabe |
| `admin.f` | administration |
| `revision.g` | revision |

Technisches Konto für ein Fachsystem: Client `normacase-host`, Secret `synthetic-host-secret`, Rolle `integration` (Client Credentials).

Im Betrieb ersetzt der Identitätsanbieter des Hauses diesen Realm. NormaCase erwartet ein Access Token mit Audience `normacase-api`, den Rollen im Claim `roles` und `preferred_username`.
