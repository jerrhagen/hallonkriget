# Buildings

Byggnader på kartan: byggplats, byggande, in- och utlager och recept. Se docs/produktionskedjor.md (Alla byggnader).

- `Building`: en byggnad. Dörren är rutan mitt under huset. En byggplats tar emot material (`MaterialNeeded`, `DeliverMaterial`) och byggarbete (`AddWork`, ett tick åt gången); hantlangarna kan bara bygga så långt materialet räcker. En färdig byggnad har inlager och utlager med högst 5 av varje vara (`InputSpace`, `PutInput`, `OutputCount`, `TakeOutput`). Förråd har ett gemensamt lager för allt.
- Produktion: när arbetaren är på plats (`HasWorker`), inlagret räcker och utlagret har plats startar en omgång. Byggnader med flera recept turas om, om spelaren inte valt ett (`SelectRecipe`) eller datan valt ett från början (`default_recipe`). Samlare behöver sin terräng inom radien från dörren, och ett recept kan ha en egen (bärplockaren: hallon eller kårt). Mjölkpallen (`daily`) gör ett byte per speldag, inte på söndagar.
- Det som tar slut: en ruta sten ger 6, en ruta skog 4 träd, en skrothög 12, sedan blir rutan glänta (`GameState.TryGather`, `GameMap.TakenAt`). Skogshuggaren planterar en gran per två fällda.
- Åkern har sex odlingsrutor till höger om huset (`field`). De går att gå över men inte att bygga eller lägga stig på.
- Placering och kommandon (`PlaceBuilding`, `SelectRecipe`) ligger i `GameState`. Reglerna för var man får bygga står i `GameState.CanPlace`.

Inte här än: rivning, prioritet. Logen, vedtraven, hundkojan och hundgården finns i datan men gör inget förrän striden kommer i fas 3.
