# Lokaler synthetischer Review-Host

Der normale NormaCase-Preview bleibt standardmäßig anonym und schreibgeschützt.
Der persistente Review-Host ist ein expliziter Entwicklungsmodus für ausschließlich
synthetische Fälle.

Er benötigt:

- `SyntheticReview__Enabled=true`
- `SyntheticReview__Credential=<kanonisches Base64 von 32 Zufallsbytes>`
- `SyntheticReview__ConnectionString=<lokale PostgreSQL-Verbindung>`

Die Zugangsinformation ist ein gemeinsamer lokaler Bootstrap-Schlüssel, keine
produktive Benutzeridentität oder fachliche Freigabeberechtigung.

## Credential erzeugen

PowerShell:

```powershell
$bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
$env:SyntheticReview__Credential = [Convert]::ToBase64String($bytes)
$env:SyntheticReview__Enabled = "true"
```

Linux/macOS:

```sh
export SyntheticReview__Credential="$(openssl rand -base64 32)"
export SyntheticReview__Enabled=true
```

Das Credential gehört nicht in Git, URLs, Screenshots oder Fall-JSON. Clients senden
es ausschließlich als `Authorization: Bearer <credential>`.

## PostgreSQL

Beispiel für eine lokale Entwicklungsdatenbank:

```sh
docker run --rm --name normacase-review-db \
  -e POSTGRES_DB=normacase_review \
  -e POSTGRES_USER=normacase_review \
  -e POSTGRES_PASSWORD=synthetic-local-password \
  -p 5432:5432 postgres:18.6
```

Setze anschließend:

```text
SyntheticReview__ConnectionString=Host=127.0.0.1;Port=5432;Database=normacase_review;Username=normacase_review;Password=synthetic-local-password;Pooling=true
```

Ohne aktivierten Modus werden die persistenten Review-Endpunkte nicht registriert.

## Endpunkte

- `GET /api/review-session`
- `GET /api/review/work-queues`
- `GET /api/review/work-cases/{caseId}`
- `POST /api/review/work-cases/{caseId}/reviews`

Die vier demo-g-Fälle werden einmalig als immutable Assessments plus Review-Aggregat
initialisiert. Neustarts lesen den committed PostgreSQL-Stand weiter.

Ein Review-Request enthält nur erwartete Revisionen, Disposition, Begründung und bei
Override ein generisches Ergebnis. Actor, Assessment-ID, Review-ID und UTC-Zeitpunkt
werden serverseitig gebunden.

```json
{
  "expectedCaseRevision": "1",
  "expectedProcessRevision": "1",
  "expectedAuditRevision": "1",
  "disposition": "ACCEPT_SYSTEM_RESULT",
  "reason": "Synthetische Freigabe"
}
```

Die synthetische Policy erlaubt Accept/Override nur im Zustand
`awaiting-approval`. Stale Revisionen liefern HTTP 409; unzulässige Zustände 403.
Es gibt keinen stillen Retry gegen einen neueren Fallstand.

Die Browser-Oberfläche für eine maskierte, nur im Speicher gehaltene Credential-Eingabe
folgt separat in #138. Produktive Identity-/Privacy-/Deployment-Freigabe bleibt #119.
