# Ai

Datorspelaren: byggordning från data/ai plus tre reflexer (mat, försvar, anfall). Ger sina kommandon samma väg som människor. Fas 3.

Det som finns nu: `BuildOrder` läser en byggordning från data/ai, och `BuildOrderPlayer` spelar upp den för en spelare som kommandon, ett tick i taget. Ett steg ges tidigast vid sin `minute`, efter `when_done` (en byggnad är färdig) och `when_produced` (så många av en vara är gjorda eller köpta), och ett steg som väntar håller kvar dem efter. Byggnader får namn i byggordningen så att senare steg kan välja recept, byten och utbildningar i dem. Används av sim.cli och fas 1:s kontrollfråga.

Se docs/designdokument.md (Datorspelaren).
