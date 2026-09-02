# Automation V1

## Verantwortung

FlurNetz.Modules.Automation ist eine persistierte, betreiberkonfigurierbare Rule Engine.
Sie verarbeitet ausschließlich die vorhandenen Integration Events
"engagement.message-recorded" v1 und "shop.purchase-completed" v1:

Integration Event → AutomationTriggerSnapshot → Conditions → Actions → AutomationExecution

Konkrete Regeln liegen ausschließlich in PostgreSQL. Der Code kennt nur die explizit
unterstützten Trigger-, Condition- und Action-Typen. Für `overlay.alert` referenziert
Automation ausschließlich `FlurNetz.Modules.Overlay.Contracts`; die Overlay-Implementierung
bleibt außerhalb der Modulgrenze. Automation.Contracts bleibt in V1 leer.

## Domain und Lifecycle

AutomationRule ist ein echtes Aggregate mit serverseitiger AutomationRuleId, kanonischem
DisplayName/Description, Trigger, Conditions, Actions, SortOrder und Lifecycle-Zustand. Neue
Rules sind deaktiviert und nicht archiviert. Archive ist terminal und deaktiviert die Rule.
Enable, Disable und Archive sind idempotent, wobei Enable für archivierte Rules und Replace für
aktivierte oder archivierte Rules fachlich abgewiesen werden.

Conditions sind ausschließlich AND-verknüpft; eine leere Liste bedeutet Match All. V1 erlaubt
genau "community-identity.equals", "shop.offer-id.equals", "shop.item-definition-id.equals",
"shop.price-paid.at-least" und "shop.price-paid.at-most". Engagement akzeptiert nur die
Identity-Condition. Actions sind "economy.credit", "notification.create" und "overlay.alert";
sie werden über lückenlose Positionen ab null deterministisch ausgeführt. `overlay.alert`
besitzt explizite Channel-, Titel-, Nachrichten-, Varianten- und Duration-Felder und verwendet
exakt die Overlay-V1-Grenzen. Economy-Beträge sind positiv,
Notification-Titel und optionale Nachrichten streng validiert und es gibt keine Templates oder
Variableninterpolation.

Alle Texte werden nach Unicode-Skalarwerten begrenzt, kanonisch getrimmt und auf U+0000 sowie
beschädigtes UTF-16 geprüft. Rehydrate validiert persistierte Werte erneut und repariert
keine beschädigten Zustände.

## Runtime und Atomizität

Die beiden expliziten Consumer heißen:

- automation.engagement-message-recorded
- automation.shop-purchase-completed

Sie projizieren ihre fremden Contracts zuerst auf einen Automation-eigenen Snapshot. Die Rule
Engine liest keine fremden Tabellen. Aktive Rules werden über sort_order ASC, id ASC
verarbeitet; die Runtime lädt sie innerhalb der bestehenden Messaging-Transaktion mit
PostgreSQL FOR SHARE. Management-Mutationen sperren die Root-Zeile mit FOR UPDATE.

IAutomationRuntimeStore erhält Connection und Transaction aus dem Messaging-Handler-Kontext,
öffnet keine eigene Connection und führt keinen Commit aus. Vor jeder Action-Gruppe wird eine
Execution mit UNIQUE (automation_rule_id, trigger_message_id) reserviert. Inbox,
AutomationExecution, alle Economy-Credits und alle Notifications werden dadurch als eine
gemeinsame Messaging-Transaktion committed oder zurückgerollt. Retry und Poison-Verhalten
bleiben vollständig beim vorhandenen OutboxProcessor.

"economy.credit" verwendet ausschließlich IEconomyBalanceCredit. "notification.create"
verwendet ausschließlich ICommunityNotificationCreate und setzt NotificationType auf
"automation.rule-executed", SourceType auf "automation.execution" und SourceId auf die
Execution-ID. Beide Capabilities verwenden die Caller-Transaktion. `overlay.alert` verwendet
`IOverlayAlertPublish` mit derselben Connection und Transaction. Ein fehlender, deaktivierter
oder archivierter Zielchannel liefert einen Suppression-Status und wird nicht zum Poison-
Fehler; ein echter Persistenzfehler rollt die gemeinsame Transaktion zurück.

## Persistenz

Die Automation-Migrationen sind:

Automation:1:CreateAutomationRulesAndExecutions
Automation:2:AddOverlayAlertAction

Migration 1 erzeugt exakt automation_rules, automation_rule_conditions,
automation_rule_actions und automation_executions. Foreign Keys zeigen ausschließlich
innerhalb dieses Automation-Schemas; Identity-, Shop-, Notifications- und Economy-IDs bleiben
fachliche Referenzen ohne Cross-Module-FK. Die Execution-History ist über
automation_rule_id, executed_at_utc DESC, id DESC indiziert.

Migration 2 ergänzt eigene Overlay-Spalten für `automation_rule_actions` und ersetzt die
Value-Shape-Constraints. Migration 1 bleibt unverändert; es gibt keinen Cross-Module-FK
zum Overlay-Channel.

IAutomationRuleStore eröffnet für Management-Operationen eigene Transaktionen und verwendet
für atomare Mutationen SELECT FOR UPDATE. Die History verwendet Keyset-Pagination ohne
Offset; der opake, versionierte Base64URL-Cursor ist an die Rule-ID gebunden.

Die PostgreSQL-Integrationstests prüfen die Sperrwirkung mit getrennten Connections und
Transaktionen: Eine laufende Runtime-Transaktion mit FOR SHARE blockiert Disable mit FOR UPDATE
bis zum Commit der Runtime. Replace wird unter demselben Lock ebenfalls bis zur Freigabe
blockiert und liefert danach bei einer weiterhin aktivierten Rule den fachlichen Konflikt; die
zulässige Disable-→Replace-Transition wird anschließend separat verifiziert. Alle Lifecycle-
Zeitpunkte werden explizit als kanonische UTC-Microsecond-Werte übergeben; Domain und Persistence
verwenden keine Systemzeit direkt.

## Management-API

Die interne API-Grenze liegt unter /api/admin/automation/rules:

GET /api/admin/automation/rules
GET /api/admin/automation/rules/{ruleId}
POST /api/admin/automation/rules
PUT /api/admin/automation/rules/{ruleId}
POST /api/admin/automation/rules/{ruleId}/enable
POST /api/admin/automation/rules/{ruleId}/disable
POST /api/admin/automation/rules/{ruleId}/archive
GET /api/admin/automation/rules/{ruleId}/executions

HTTP-DTOs gehören ausschließlich zur API. Create/Replace liefern 201, Get/List 200,
erfolgreiche Mutationen 204, ungültige Eingaben 400, unbekannte Rules 404 und fachliche
Konflikte 409 als ProblemDetails. Die Rule-Liste ist vollständig und nach
SortOrder/ID sortiert. Die Execution-History verwendet standardmäßig 50, maximal 100 Einträge
pro Seite. Delete, Run Now, Dry Run, Replay und Backfill existieren bewusst nicht.

## Composition und Tests

AddAutomationModule() registriert Clock, Stores, Use Cases, Engine, History und Migration.
AddAutomationConsumers() registriert ausschließlich die beiden expliziten Consumer.
Der API-Host bindet nur Management und Migration ein; der Worker bindet zusätzlich Economy
Credit, Notification Create und die beiden Consumer in den bestehenden Outbox-Loop ein.

Unit-, Architecture-, PostgreSQL-Integration-, Workflow-, Worker- und API-Tests decken Domain-
Invarianten, Migration/Checks/FKs, Locking, deterministische Sortierung, Reservation,
Keyset-History, Duplicate Delivery, Multi-Action-/Multi-Rule-Rollback, den realen Shop-Kauf
und die laufende Worker-Verarbeitung ab. Cron, Scheduler, Delay, OR/NOT, Scripts, Templates,
weitere Eventtypen, eigene Queues, eigenes Retry und ein Admin-Frontend sind nicht Bestandteil
von Automation V1.
