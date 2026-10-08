# Data

Läser data/goods.json och data/buildings.json till `GameData`. Varornas och byggnadernas nummer är deras plats i filerna. Fel i filerna ger ett `GameDataException` med en förklaring.

`GameData.Fingerprint` är en kontrollsumma över allt som påverkar matchen (inte namnen) och ingår i `GameState.Hash()`, så två datorer med olika data märker det direkt.
