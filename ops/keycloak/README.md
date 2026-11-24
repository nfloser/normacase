# Keycloak-Testrealm

`normacase-realm.json` wird beim Start von `docker compose up` importiert. Alle Konten sind synthetisch und nur für die lokale Vorführung gedacht. Passwort jeweils `demo`. Keycloak akzeptiert Benutzernamen und E-Mail-Adressen unabhängig von der Groß- und Kleinschreibung.

Für das erste Konto setzt Compose standardmäßig `tobias.floeser@synthetisch.invalid`. Für eine lokale Demo-Adresse `NORMACASE_DEMO_REVIEWER_EMAIL` in der ignorierten `.env` neben `compose.yaml` setzen. Änderung an einer laufenden Demo mit `docker compose up -d --force-recreate keycloak` übernehmen. Keycloak importiert den Demo-Realm frisch, bestehende Anmeldungen verfallen und die getrennte Vorgangsdatenbank bleibt erhalten.

| Benutzer | Rolle |
|---|---|
| `tobias.flöser` (Tobias Flöser), `gutachter.b` (Bernd Krause) | gutachter |
| `freigabe.c` (Clara Wendt) | freigabe |
| `redaktion.d` | regelwerk-redaktion |
| `regelwerk.e` | regelwerk-freigabe |
| `admin.f` | administration |
| `revision.g` | revision |

Technisches Konto für ein Fachsystem: Client `normacase-host`, Secret `synthetic-host-secret`, Rolle `integration` (Client Credentials).

Im Betrieb ersetzt der Identitätsanbieter des Hauses diesen Realm. NormaCase erwartet ein Access Token mit Audience `normacase-api`, den Rollen im Claim `roles` und `preferred_username`.
