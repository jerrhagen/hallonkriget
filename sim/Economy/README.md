# Economy

Leveranssystemet (`GameState.Deliveries.cs`), som i designdokumentet: begäran, erbjudande och bärare. Var 5:e tick:

1. Byggplatser i den ordning de placerades får sitt material, hela vägen för den första innan nästa.
2. Färdiga byggnader begär det inlagret har plats för. De tas i omgångar, en vara per byggnad och omgång, och den som börjar flyttas ett steg varje gång, så att en knapp vara delas.
3. Varje begäran får närmaste erbjudande (producenter före förråd) och den lediga bärare som står närmast varan.
4. Det ingen begär bärs till närmaste förråd, men bara tills förrådet har en fjärdedel av sin plats av den varan.

En `Delivery` reserverar varan i avsändaren (`Outgoing`) och platsen hos mottagaren (`Incoming`). Tar mottagaren inte emot varan när bäraren kommer fram, bärs den till närmaste förråd.

Kafferepet och bygdegården tar emot varor som en vanlig byggnad. Bordet rymmer 40 portioner av all mat tillsammans. Lanthandeln är en byggnad vars recept är byten från `trade.json`; den gör ingenting förrän spelaren valt ett, och är stängd på söndagar (`GameClock`).

En byggnad kan spärra en vara (`BlockGood`), så att den inte begärs. Kaffet är spärrat på kafferepet från början.

Inte här än: prioritet per byggnad, spärrar i boden (att inte lämna ut), vägar som cachas mellan byggnader.
