# Data

Läser data/goods.json, buildings.json, food.json (humör och mat), professions.json (yrken och verktyg) och trade.json (lanthandelns priser) till `GameData`, med `GameData.FromFiles`. Varornas och byggnadernas nummer är deras plats i filerna. Fel i filerna ger ett `GameDataException` med en förklaring.

`GameData.Fingerprint` är en kontrollsumma över allt som påverkar matchen (inte namnen) och ingår i `GameState.Hash()`, så två datorer med olika data märker det direkt.
