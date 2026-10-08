# Beslut

Beslut som tagits efter designdokumentet. Nyast överst.

## 2026-10-08: Snålare CI

- Repot är privat och Actions-minuterna är få. Pull requests kör bara Linux (tester och spelbygge, en minut). Windows-jämförelsen av determinismen körs vid push till main och för hand. Ändringar som bara rör grafik eller dokument kör inga tester.

## 2026-10-08: Stilprovet

- Kontrollfrågan för fas 0 är besvarad ja: Aron tycker att stilprovet "ser fint ut". Stilen gäller, fas 1 börjar.

- Renderaren är `gl_compatibility`, så att spelet går på äldre datorer och utan grafikkort i CI.
- Grafiken renderas i 1× (128 px per ruta) och 2×. Spelet använder 2× nerskalad till hälften, med mipmaps, så att den är skarp både inzoomad och utzoomad.
- Klippdockor byggs som platta `Node2D`-delar med pivotpunkter från `*_rig.json`, inte med `Skeleton2D`. Animationerna nycklas i kod i 12 bilder per sekund.
- Pysslingarna i stilprovet går bara i vyn, inte genom simuleringen. Kopplingen sim–vy kommer i fas 1.
- Stigen i stilprovet är en enda dekal. De riktiga stigarna (16 rutvarianter) kommer i fas 2.

## 2026-10-08: Fas 0 startar

- Repot är `jerrhagen/hallonkriget`, privat. Grundstrukturen läggs direkt på `main`, resten kommer som pull requests.
- Godot-versionen låses till 4.4.1 .NET. Simuleringskärnan är .NET 8.
- Dokumenten i `docs/` är exporterade från designdokumentet den 8 oktober 2026. Originalet (med diagrammen) är Claude-dokumentet "Hallonkriget – designdokument och implementationsplan".
