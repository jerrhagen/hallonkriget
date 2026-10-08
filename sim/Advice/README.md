# Advice

Sixten (Torpet) eller Major (Storgården) i hörnet. `Advisor.Update(state)` anropas efter varje tick och ger ibland en `Remark`: en varning när ett läge har hållit i sig i 20 sekunder (en som ger upp sägs direkt), och annars en pratbubbla var tredje minut. Då upprepas det viktigaste läget som fortfarande gäller, eller så kommer pratet om det är påslaget. Pratet kan slås av med `Chatter`, varningarna inte.

Varningarna: inget kafferep, tomt bord, folk nära att ge upp, någon gav upp, lite kaffe på lördagen, en byggnad utan arbetare där ingen med yrket finns, inga bärare, alla bärare upptagna, ett fullt förråd och inga sängar när någon står i kö i bygdegården.

Replikerna står i `data/radgivare.json`. Rådgivaren läser bara tillståndet och är inte en del av det, så den finns inte i `Hash()`.
