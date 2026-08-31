# Identity

## Verantwortung

`FlurNetz.Modules.Identity` besitzt die zentrale interne Identität eines Community-Mitglieds.
Andere Module verwenden später ausschließlich diese interne Identität und nicht die Kennung
einer externen Plattform.

Die vorgesehene Trennung lautet:

`External Identity` → `Resolution/Mapping` → `CommunityIdentityId`

Twitch-, Discord-, YouTube- und Streamer.bot-Kennungen bleiben damit Integrations- oder
Mappingdaten und ersetzen niemals die interne FlurNetz-Identität.

## Aktueller Vertical-Slice-Stand

`FlurNetz.Modules.Identity.Contracts` bildet die öffentliche Grenze und enthält
`CommunityIdentityId` sowie die schmale caller-neutrale
`ICommunityIdentityExistence`-Capability. Der unveränderliche Identifier basiert auf `Guid`,
weist `Guid.Empty` zurück und kann für neue interne Identitäten über `New()` erzeugt werden.

`ICommunityIdentityExistence` prüft eine bereits aufgelöste `CommunityIdentityId` innerhalb
einer vom aufrufenden Slice bereitgestellten `DbConnection` und `DbTransaction`. Der Contract
führt keinen Commit aus und veröffentlicht weder Repository- noch Identity-Domainobjekte. Der
erste reale Aufrufer ist der Shop-Purchase-Slice, der die Existenzprüfung damit in dieselbe
PostgreSQL-Transaktion wie Debit, Inventory-Grant, Purchase und Outbox einbindet. Identity kennt
Shop dabei nicht.

`FlurNetz.Modules.Identity` enthält die minimale Domain-Identität `CommunityIdentity`. Sie
trägt ausschließlich eine gültige `CommunityIdentityId`; ihre ID kann nach der Erzeugung nicht
verändert werden. Der Use Case `CreateCommunityIdentity` erzeugt eine neue interne ID, bildet
damit eine Domain-Identität und persistiert sie. Die Identität kann anschließend über den
moduleigenen Repository-Port geladen werden.

Der Dapper-/Npgsql-Adapter liegt innerhalb der Identity-Implementierung und verwendet die
technische `PostgreSqlTransaction`. Die erste fachliche Migration gehört Identity und legt
`community_identities` mit genau der UUID-Primärschlüsselspalte `id` an. Die Migration wird
über den bestehenden SQL-first Migration Runner ausgeführt und in dessen History unter
`Identity:1:CreateCommunityIdentities` nachverfolgt.

`Identity.Contracts` bleibt bewusst auf den öffentlichen Identifier und die gezielte
Existenz-Capability begrenzt. Der allgemeine Persistenz-Port, Create-Use-Case, die
Domain-Identität und die Migrationsquelle sind keine Cross-Module-Verträge. Die konkrete
`CommunityIdentityExistence`-Implementierung liest ausschließlich
`community_identities` über die vom Aufrufer übergebene Transaktion.

Andere Module verwenden weiterhin die interne `CommunityIdentityId`; externe
Plattformkennungen werden erst an einer späteren Resolution-/Mapping-Grenze zugeordnet.

## HTTP-Adapter

Der bestehende `CreateCommunityIdentity`-Use-Case ist jetzt über den API-Adapter
`POST /api/identities` erreichbar. Der Endpunkt erzeugt keine eigene Identität, sondern
ruft ausschließlich den Use Case auf; die HTTP-Grenze übersetzt dessen
`CommunityIdentityId.Value` in ein API-Response-DTO. Details zur Host-Konfiguration und zum
Response-Vertrag stehen in [api.md](api.md).

Plattformkonten, Authentifizierung, Profile, fachliche Domain- oder Integration Events und
Messaging-/Outbox-Runtime bleiben weiterhin außerhalb dieses Slices.
