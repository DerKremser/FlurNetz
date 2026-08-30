# Progression Foundation

Progression besitzt den fachlichen Fortschritt einer internen Community-Identität. In
dieser Foundation ist der einzige modellierte Fortschrittswert der angesammelte Wert
`ExperiencePoints`. Progression verwendet dafür ausschließlich die bestehende
`CommunityIdentityId` aus `FlurNetz.Modules.Identity.Contracts` und führt keine eigene
Benutzer-ID ein.

`ExperiencePoints` ist ein unveränderlicher, auf `long` basierender Value Type. Zero ist
gültig, negative Werte sind verboten und eine Addition oberhalb von `long.MaxValue` wird
als `OverflowException` sichtbar abgelehnt. `CommunityProgression` gehört genau einer
gültigen `CommunityIdentityId`, startet mit `0` XP und kann positive XP-Vergaben
akkumulieren.

`FlurNetz.Modules.Progression.Contracts` bleibt bewusst leer, weil dieser Foundation-
Schritt noch keinen realen Cross-Module-Vertrag benötigt. Die Domain-Typen bleiben in
der Implementierungs-Assembly.

Noch nicht Bestandteil der Foundation sind Level oder Level-Berechnung, Persistence,
Messaging, Engagement-Kommunikation, Events, Rewards und API-Endpunkte. Auch die
automatische Erstellung oder Persistierung eines Progressionszustands wird erst mit dem
ersten echten Progression-Vertical-Slice entschieden.
