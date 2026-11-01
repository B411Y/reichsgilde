# Herkunft: Kulissenberge

`kulisse_koeln.glb` (Kulissenberge um Köln) entsteht aus eigenen Skripten (`tools/modelle/kulisse_hoehen.py`,
`tools/modelle/kulisse_koeln.py`). Die Form der Berge folgt echten Höhendaten:

- **Quelle:** Digitales Geländemodell (DGM) des Landes Nordrhein-Westfalen, Geobasis NRW (Bezirksregierung Köln),
  Web Coverage Service `https://www.wcs.nrw.de/geobasis/wcs_nw_dgm`, Abdeckung `nw_dgm` (EPSG:25832).
- **Lizenz:** **Datenlizenz Deutschland – Zero – Version 2.0** (`dl-de/zero-2-0`), Lizenztext:
  https://www.govdata.de/dl-de/zero-2-0. Wortlaut im Dienst selbst (GetCapabilities, `Fees`): „Es gelten die durch den
  IT-Planungsrat im Datenportal für Deutschland (GovData) veröffentlichten einheitlichen Lizenzbedingungen ‚Datenlizenz
  Deutschland – Zero‘ (https://www.govdata.de/dl-de/zero-2-0). Jede Nutzung ist ohne Einschränkungen oder Bedingungen
  zulässig.“ Kommerzielle Nutzung, Veränderung und Weitergabe sind also ohne Namensnennung erlaubt; ein Quellenvermerk ist
  nicht Pflicht, steht hier aber trotzdem.
- **Abruf:** 01.11.2026. Umland 100 × 100 km um Köln (Ost 306–406 km, Nord 5.595–5.695 km) in 16 Kacheln zu 25 km,
  vom Dienst auf 100-m-Pixel gemittelt; dazu je Gipfel mit Namen eine Kachel 2,4 × 2,4 km in 5-m-Pixeln (Gipfelhöhe).
  Die Daten selbst liegen nicht im Repository; abgeleitet sind `tools/modelle/kulisse_koeln_profil.json` (höchste Höhe je
  halbem Grad Peilung und Staffel), das Modell und `game/daten/berge.json` (Namen, Lage, Höhe).
- **Bearbeitung:** stark vereinfacht – je Peilung nur die höchste Höhe dreier Entfernungsbänder (6–16, 16–27, 27–49 km),
  waagrecht auf einen Ring 0–300 m hinter dem Kartenrand gestaucht, Höhe × 0,5, Flanken geglättet, Wald und Fels als
  Vertexfarbe. Tagebaue und Halden des Braunkohlereviers (Hambach, Garzweiler, Inden, Sophienhöhe, Glessener und Vollrather
  Höhe, Fortuna, Frechen) und alle Höhen unter 30 m ü. NN zählen nicht – es gab sie um 1400 nicht.
- **Nur NRW:** Gebraucht werden nur Daten aus Nordrhein-Westfalen. Wo die Bänder nach Rheinland-Pfalz reichen (Süden,
  Ahr und Westerwald), liefert der Dienst keine Höhe; dort zählt nur der NRW-Teil. Rheinland-Pfalz (LVermGeo) stellt sein
  DGM unter „Datenlizenz Deutschland – Namensnennung 2.0“ bereit (kommerziell erlaubt, aber mit Pflicht zum Vermerk
  „©GeoBasis-DE / LVermGeoRP <Jahr>, dl-de/by-2-0“ und Hinweis auf Veränderung) – es wird hier **nicht** verwendet.

Das Modell selbst ist eigene Arbeit (Skripte MIT, Modell nach dem Stichtag „Alle Rechte vorbehalten“, siehe
`LICENSE-INHALTE.md`); die Höhendaten darin sind gemeinfrei nutzbar (dl-de/zero-2-0).

Herkunft je Datei aller Assets (auch dieser): `docs/assets/protokoll.csv`.
