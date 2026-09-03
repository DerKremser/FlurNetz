# Administration V1

Administration V1 ist die lokale, cookie-basierte Betreibergrenze von FlurNetz. Sie besitzt
nur administrative Zustände: Credentials, die statische Administrator-Rollenzuweisung,
Audit-Einträge und idempotente Operationen. Fachliche Daten bleiben im jeweiligen Owner-Modul.

## Host- und Modulgrenze

`FlurNetz.Api` ist die Composition Root für die Administration. Das API-Projekt registriert
Administration, Identity, Economy, Progression, Inventory, Achievements, Titles, Rewards,
Shop, Notifications, Automation, Integrations und Overlay sowie deren vorhandene
Migrationen. Der API-Prozess ist weiterhin nur HTTP-Host und Outbox-Producer: Er startet keine
Worker-Schleife, keinen Outbox-Processor und keine Automation- oder anderen Messaging-
Consumer.

`FlurNetz.Modules.Administration` referenziert nur die eigenen Contracts, Identity.Contracts
für die öffentliche Identity-Capability sowie die technische Persistence Foundation.
Fremde Fachzustände werden nicht gespiegelt und nicht per SQL gelesen oder verändert. Für
atomare Owner-Mutationen stellt das jeweilige Owner-Modul eine geeignete transaction-aware
Capability bereit; die Administration kennt weder fremde Stores noch fremde
Implementierungsassemblies.

## Eigener Zustand und Migration

`Administration:1:CreateAdministrationState` legt im `public`-Schema ausschließlich diese
Administration-eigenen Tabellen an:

- `administration_credentials`
- `administration_role_assignments`
- `administration_audit_entries`
- `administration_operations`
- `administration_setup_state` als Singleton für den First-Run-Abschluss

Es gibt keine Cross-Module-Foreign-Keys, insbesondere keinen Foreign Key auf
`community_identities`. Eine neue Identity für den First-Run wird ausschließlich über den
öffentlichen `ICommunityIdentityCreator`-Vertrag des Identity-Moduls angelegt. Der
unveränderte MigrationRunner verwaltet Identität, Reihenfolge und SQL-Checksum.

Credentials enthalten `CommunityIdentityId`, Email, NormalizedEmail, einen
ASP.NET-Core-PasswordHasher-Hash, CredentialVersion sowie Erstellungs- und
Passwortänderungszeitpunkt. Email ist case-insensitive eindeutig und wird für den Lookup
kanonisch normalisiert. Passwörter werden mit dem etablierten Microsoft-Hasher verarbeitet: 15 bis 128
Zeichen, ohne Trim, Case- oder Unicode-Normalisierung und ohne eigene Kryptographie.

## First-Run-Setup und Recovery

`GET/POST /admin/setup` ist ein anonymer, einmaliger First-Run-Flow. Er ist nur verfügbar,
solange `administration_setup_state.completed_at_utc` leer ist und noch kein Credential mit
Administrator-Rolle existiert. Die Konfiguration `Administration:Setup:Secret` kommt aus
User-Secrets oder der Umgebung; sie wird nur zur Laufzeit gehalten und nie persistiert,
geloggt, auditiert oder als Operation registriert. Fehlt das Gate oder stimmt es nicht,
bleibt der Setup-Versuch ohne Datenbankeffekt und zeigt nur einen generischen Fehler.

Der Setup-Service sperrt die Singleton-Zeile mit `FOR UPDATE`, erzeugt die neue
`CommunityIdentity` über `ICommunityIdentityCreator` und schreibt Identity, Credential,
Administrator-Rolle sowie den Abschlussstatus in derselben PostgreSQL-Transaktion. Dadurch
gewinnt bei parallelen First-Run-Requests genau ein Vorgang; danach antwortet der Flow mit
`404 Not Found`. Der Setup-POST ist Anti-Forgery- und rate-limit-geschützt und setzt
`Cache-Control: no-store`; Passwörter, Setup-Gate und E-Mail werden nicht geloggt oder in
Audit/Operations geschrieben. Der Login erfolgt anschließend mit der E-Mail-Adresse.

Operational Recovery ist als Application-Service vorhanden und kein Forgot-Password-Webflow.
Sie verlangt explizite Identity, neues Secret, neue RequestId und eine vorhandene
Administrator-Rolle. Die RequestId ist idempotent; eine zweite Ausführung derselben Recovery
ändert weder Hash noch Version. Erfolgreiche Recovery erhöht CredentialVersion und schreibt
ein High-Risk-Audit ohne Secret, Passwort oder Hash.

## Authentication und Session

Die Webgrenze verwendet das eigene ASP.NET-Core-Cookie-Scheme `FlurNetz.Admin` mit
`__Host-FlurNetz.Admin`. Das Cookie enthält nur Identity-ID, Email, CredentialVersion und
Scheme-Kennung. Es ist HttpOnly, SameSite Strict und in Production Secure. Die Idle-Lifetime
beträgt 30 Minuten; eine serverseitig geprüfte absolute Grenze beendet Sessions spätestens
nach acht Stunden. Es gibt kein Remember Me, kein JWT und keinen LocalStorage-Token.

`GET /admin/login` zeigt das Loginformular, `POST /admin/login` normalisiert die E-Mail,
führt immer einen sicheren Dummy-Verify-Pfad für unbekannte Adressen aus und verlangt eine
Administrator-Rolle. Alle Fehler lauten einheitlich „Anmeldedaten sind ungültig.“. Der Login
ist über die vorhandene ASP.NET-Core-Rate-Limit-Infrastruktur auf zehn Versuche pro Minute
und Quelladresse begrenzt. `POST /admin/logout` ist die einzige Logout-Methode.

Bei jedem authentifizierten Request werden Credential, CredentialVersion, Email und
Administrator-Rollenzuweisung serverseitig validiert. Passwortänderung unter
`/admin/account` prüft das aktuelle Passwort, erzwingt die Password Policy, erhöht die
CredentialVersion, schreibt nur `Administration.CredentialChanged` mit
`CredentialChanged=true` und stellt die aktuelle Session neu aus. Alte Sessions werden damit
ungültig. `/admin/setup` ist ausschließlich der anonyme First-Run-Flow und nach dem ersten
Administrator dauerhaft geschlossen.

## Permissions und Policies

Der stabile V1-Katalog liegt in `PermissionCatalog`. Die Rolle `Administrator` ist ein
versioniertes statisches Bundle mit allen V1-Permissions. Es gibt keine Role- oder
Permission-Management-UI.

Jede Admin-Ressource verwendet eine explizite Policy im Format `Admin.<Permission>`. Jede
Capability-Policy verlangt sowohl `Administration.Access` als auch die konkrete Permission.
Die Authorization-Handler prüfen zusätzlich serverseitig die gültige Admin-Principal-
Semantik. Für `/api/admin/...` liefert fehlende Authentifizierung `401` beziehungsweise
fehlende Berechtigung `403`, ohne HTML-Loginredirect. Webpfade dürfen zur Login- oder
Forbidden-Seite weiterleiten.

Alle cookie-authentifizierten Mutationen sind Anti-Forgery-geschützt: Razor-POSTs, Login,
Logout und die Minimal-API-POST-, PUT- und DELETE-Routen. Der API-Nachweis verwendet den
Header `X-CSRF-TOKEN`; unsichere Adminpfade werden zusätzlich zentral validiert. Es gibt
keine GET-Mutation.

## Audit, Operations und Atomizität

`AdminExecutionContext` enthält ActorCommunityIdentityId, ActorEmail, CorrelationId und
die aktuelle Permission-Menge. Die API leitet die Correlation aus Activity TraceId oder dem
HTTP-TraceIdentifier ab; Application-Code greift nicht auf HttpContext zu.

High-Risk-Mutationen verlangen eine nichtleere Begründung und eine eindeutige RequestId. Die
semantische Operation-Fingerprint bindet RequestId, Operation, Ziel und relevante Nutzdaten
an SHA-256. Eine Wiederholung mit demselben Fingerprint liefert das gespeicherte Ergebnis;
eine Wiederholung mit anderem Fingerprint ist ein Idempotency-Konflikt und mutiert den Owner
nicht. Concurrent Reservierungen warten nicht und liefern einen deterministischen
In-Progress-Konflikt.

Die `AdminMutationCoordinator`-Transaktion umfasst Reservation, Owner-Mutation und
Success-Audit. Auch normale, nicht idempotente Katalogänderungen laufen über die gemeinsame
Audit-Transaktion. Scheitert Audit oder Owner-Mutation, wird der gesamte Vorgang inklusive
AdminOperation zurückgerollt. Audit-Summaries werden redigiert; Schlüssel mit Passwort-,
Hash-, Secret- oder Token-Bezug werden nie persistiert.

## Fachliche Adminbereiche

Die API stellt permission-geschützte, owner-owned Reads und Mutationen bereit für:

- Identity-Liste mit Keyset-Cursor und Identity-Detail
- Economy lesen sowie Credit/Debit
- Progression lesen und positive XP-Grants
- Inventory lesen sowie positive Add/Remove-Mengen
- Achievement-Definitionen verwalten und Community-Unlocks
- Title-Definitionen verwalten sowie Community-Unlock/Lock
- Reward-Definitionen und Packages anlegen, Grants lesen und Packages vergeben
- Shop-Management über die bestehende `/api/admin/shop/offers`-Grenze
- Automation-Management über die bestehende `/api/admin/automation/rules`-Grenze
- Integrations-Mappings über die bestehende `/api/admin/integrations/external-identities`-
  Grenze
- Overlay-Channel-Management über die bestehende `/api/admin/overlay/channels`-Grenze
- Notifications-Reads innerhalb des Identity-Details
- Audit-Liste und Account-Passwortänderung

Die vorhandenen Management-Routen wurden nicht dupliziert, sondern mit Policies, CSRF,
Reason-/RequestId-Verträgen für High-Risk-Aktionen und atomaren Owner/Audit-Flows ergänzt.
Nicht enthalten sind unter anderem Bulk-Mutationen, Delete/Restore, Role Management,
Forgot-Password-Webflow, SQL-Console, Replay/Run-Now, Inventory-Item-Katalog,
Achievement-Revoke, Title-SetCurrent, Reward-Edit/Delete und Overlay-Source-Key-Readback.

## One-Time Overlay Secrets

Create und Rotate geben den zufälligen Overlay-Source-Key genau einmal in der Mutation-
Antwort aus. Persistiert wird ausschließlich der Hash. Lists, Reads, Audit, Operations und
Fehlerantworten enthalten keinen Source-Key. Die vorhandene Browser-Source bleibt auf ihren
funktionalen Pfaden erreichbar; Security-Header sind für Admin-Web- und Admin-API-Pfade
gescoped, damit die Overlay-Auslieferung nicht beschädigt wird.

## Razor-Administration

Die UI liegt in `src/FlurNetz.Api/Pages/Admin` und verwendet Razor Pages ohne SPA- oder
Frontend-Build-Pipeline. Die gemeinsame Layout-/CSS-/JavaScript-Schicht bietet Dashboard,
Identity-Liste und -Detail, Shop, Catalog für Achievements/Titles/Rewards, Automation,
Integrations, Overlay, Audit, Account, geschütztes Setup, Login und Forbidden. Account und
Setup bieten neben dem Passwortfeld einen optionalen clientseitigen Generator mit 24 Zeichen.
Er verwendet ausschließlich `window.crypto.getRandomValues()` mit Rejection-Sampling; eigene
Passphrasen bleiben erlaubt. Anzeigen/Verbergen, Kopieren und die Bestätigung sind reine
Formularfunktionen. Der Wert wird weder automatisch erzeugt noch in Local-/Session-Storage,
Cookies, Querystrings, Logs, Audit oder AdminOperations geschrieben und wird nach Reload nicht
aus FlurNetz wiederhergestellt. Fachliche Texte werden durch Razor standardmäßig encoded;
Formulare besitzen sichtbare Validierungs- und Fehlerzustände,
Labels, Tabellen-Captions und Tastaturfokus. Empty States zeigen nur echte leere Owner-Daten,
Fehler werden als „Nicht verfügbar“ behandelt und nicht als erfundene Defaultwerte.

## Tests und Abnahme

`FlurNetz.Modules.Administration.Tests` prüft Permission-Bundle, Credential- und Login-
Invarianten, Password Policy, Fingerprint, Reason und Versionierung. Das separate
`FlurNetz.Modules.Administration.IntegrationTests` verwendet echtes PostgreSQL über
Testcontainers oder `FLURNETZ_TEST_CONNECTION_STRING` und prüft Migration/Checksum,
Constraints, Role/Credential-Roundtrip, Audit, Operation-Reservation, Fingerprint-Konflikt,
Gate-geschützten First-Run, genau einen parallelen Setup-Gewinner, Recovery, atomaren
Rollback und 20-fache parallele Idempotenz.

`FlurNetz.Api.IntegrationTests` prüft den echten Cookie-Login, generische Fehler, CSRF,
First-Run-Verfügbarkeit/-Schließung, fehlendes/falsches Setup-Gate, Rate-Limit,
Logout-Methode, CredentialVersion-Revocation sowie die Regressionen der
bestehenden Management-Grenzen. Zusätzlich prüfen statische UI-Sicherheitstests den
Generator auf `window.crypto.getRandomValues()`, die Abwesenheit von `Math.random()` und
persistenten Browser-Speichern sowie die Verdrahtung beider Passwortseiten. Architekturtests sichern Contract-/Implementierungs-
Richtung, die Administration-Tabellen und das Verbot fremder SQL-Tabellen ab.
