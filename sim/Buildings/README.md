# Buildings

Byggnader på kartan: byggplats, byggande, in- och utlager och recept. Se docs/produktionskedjor.md (Alla byggnader).

- `Building`: en byggnad. Dörren är rutan mitt under huset. En byggplats tar emot material (`MaterialNeeded`, `DeliverMaterial`) och byggarbete (`AddWork`, ett tick åt gången); hantlangarna kan bara bygga så långt materialet räcker. En färdig byggnad har inlager och utlager med högst 5 av varje vara (`InputSpace`, `PutInput`, `OutputCount`, `TakeOutput`). Förråd har ett gemensamt lager för allt.
- Produktion: när arbetaren är på plats (`HasWorker`), inlagret räcker och utlagret har plats startar en omgång. Byggnader med flera recept turas om, om spelaren inte valt ett (`SelectRecipe`). Samlare (skogshuggare, stenröjare) behöver sin terräng inom radien från dörren.
- Placering och kommandon (`PlaceBuilding`, `SelectRecipe`) ligger i `GameState`. Reglerna för var man får bygga står i `GameState.CanPlace`.

Inte här än: att skogen huggs ner och stenen tar slut (People), åkerns odlingsrutor, rivning, prioritet.
