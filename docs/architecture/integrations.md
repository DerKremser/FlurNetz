# Integrations V1

Integrations V1 implementiert die persistierte External-Identity-Mapping- und
Resolution-Grenze von FlurNetz. Der Slice beantwortet die Frage, welcher internen
CommunityIdentityId eine externe Provider-/Benutzerkennung zugeordnet ist:

    External Provider + opaque External User ID
                        |
                Integrations Mapping
                        |
                 CommunityIdentityId

## Verantwortung und Grenzen

FlurNetz.Modules.Integrations besitzt ausschließlich die Mappingdaten. Die zentrale
interne Identität bleibt im Identity-Modul; externe Kennungen ersetzen
CommunityIdentityId niemals. Provider-Keys sind kanonische, stabile Strings, keine
Enum-Werte und keine dynamische Plugin-Registry. Externe Benutzerkennungen sind opaque
und werden nicht numerisch interpretiert.

Die Contracts-Assembly veröffentlicht nur IntegrationProviderKey, ExternalUserId und
die caller-neutrale IExternalIdentityResolution. Repository-, Domain-, Dapper- und
PostgreSQL-Typen bleiben implementation-intern. Ein späterer Adapter kann die
Resolution-Capability verwenden, ohne die Integrationsimplementierung oder die Identity-
Persistenz zu übernehmen.

Integrations referenziert ausschließlich Identity.Contracts für
ICommunityIdentityExistence. Beim Linken wird die Existenz der Zielidentität innerhalb
der Mapping-Transaktion geprüft. Integrations liest weder community_identities direkt
noch andere Modultabellen und erzeugt keine Community-Identität automatisch.

## Domain und Use Cases

Das Domainmodell ExternalIdentityMapping besteht aus:

- IntegrationProviderKey
- ExternalUserId
- CommunityIdentityId

Die Application-Schicht bietet LinkExternalIdentity, ResolveExternalIdentity,
GetExternalIdentityMapping, ListExternalIdentityMappings und
UnlinkExternalIdentity. Ein identisches Linken ist idempotent. Ein Link derselben
externen Identität auf eine andere Community-Identität wird als fachlicher Konflikt
abgelehnt; es gibt kein stilles Reassignment. Unlink entfernt ausschließlich die
Integrationsverknüpfung und löscht keine Identity- oder Fachdaten anderer Module.

## PostgreSQL-Persistenz

Die Migration Integrations:1:CreateExternalIdentityMappings erzeugt ausschließlich
integration_external_identity_mappings mit den Spalten provider_key,
external_user_id und community_identity_id. Der Primary Key auf
(provider_key, external_user_id) erzwingt die systemweite Eindeutigkeit der externen
Identität. Ein zusätzlicher Index unterstützt die Liste nach Community-Identität.
Es gibt keinen Cross-Module-Foreign-Key auf community_identities.

Der Link-Store verwendet PostgreSQL, Npgsql, Dapper und eine explizite Transaktion:
Identity-Existenzprüfung und Insert teilen dieselbe Transaktion; der Insert verwendet
ON CONFLICT DO NOTHING, danach wird zwischen idempotenter Wiederholung und Konflikt
unterschieden. Dadurch bleibt auch bei parallelen Link-Versuchen genau eine Zuordnung
bestehen. Es gibt kein ORM, kein Generic Repository, keine History-Tabelle und keine
neue Outbox-/Event-Kopplung.

## HTTP-Management-Grenze

Der API-Host registriert AddIntegrationsModule() und mappt:

    POST   /api/admin/integrations/external-identities
    GET    /api/admin/integrations/external-identities/{provider}/{externalUserId}
    GET    /api/admin/integrations/external-identities/community/{communityIdentityId}
    DELETE /api/admin/integrations/external-identities/{provider}/{externalUserId}

Die API verwendet eigene Request-/Response-DTOs und ProblemDetails. Ungültige Eingaben
liefern 400, eine unbekannte Community-Identität beim Link 404, ein unbekanntes
Mapping bei Get/Unlink 404 und ein Reassignment 409. Die Route ist eine interne
Management-Grenze und wird in Administration V1 über das Admin-Cookie-Scheme, explizite
Permissions, Anti-Forgery sowie Audit/Operations geschützt. Eine allgemeine Community-
Authentication und die Provider-Anbindungen bleiben außerhalb dieses Slices.

## Bewusst nicht enthalten

V1 enthält keine Twitch-Liveverbindung: kein OAuth, EventSub, Helix, Chat,
Moderation, Token-Refresh oder WebSocket. twitch ist lediglich als stabiler Provider-Key
für Mappings vorgesehen. Streamer.bot bleibt ein späterer externer Adapter und lädt keine
internen FlurNetz-Assemblies. OBS und Overlay bleiben unverändert. Ebenso gibt es keine
Razor-/MVC-Adminoberfläche, Sidebar, RBAC, Notifications, Automation-Trigger,
generische Plugin- oder Webhook-Architektur und keine Mapping-History.

## Tests

FlurNetz.Modules.Integrations.Tests prüft Value-Types, Domaininvarianten,
Idempotenz, Konflikte, Unlink und Resolution mit Fakes. Das separate
FlurNetz.Modules.Integrations.IntegrationTests-Projekt prüft Migration und Idempotenz,
Roundtrips, Identity-Existenz, Constraints und konkurrierende Links gegen echtes
PostgreSQL. FlurNetz.Api.IntegrationTests prüft die vollständige HTTP-/PostgreSQL-Grenze.
Architecture Tests sichern Contracts-Minimalität, Abhängigkeitsrichtung und fehlende
Cross-Module-Foreign-Keys.
