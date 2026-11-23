"""Zaehlt die Dreiecke der Huelle je Bauteilfunktion (ausschliesslich: Dreiecke, die untergeordnete Bauteile erzeugen, zaehlen bei diesen).

Aufruf nur zur Messung, aus den Bauskripten der Patrizierhaeuser (wohnhaus_zwerg_s23.py, wohnhaus_elb_s23.py), wenn die Umgebungsvariable
REICHSGILDE_ZAEHLEN=1 gesetzt ist: `anlegen(modul, ...)` verpackt jede Funktion, deren erster Parameter `h` heisst (Haus-Netz), und gibt am Ende
Zeilen "ZAEHLER <Funktion> <Dreiecke> <Aufrufe>" aus (atexit). Grundlage der Tabelle docs/bereiche/darstellung_dreiecke_zwerg_elb_s3.md
(tools/modelle/neubau/dreiecke_tabelle.py). Aendert nichts am Modell."""
import atexit
import functools
import inspect

_SUMME, _AUFRUFE, _STAPEL = {}, {}, []


def _verpacke(name, f):
    @functools.wraps(f)
    def g(h, *a, **kw):
        vorher = h.dreiecke()
        _STAPEL.append(0)
        try:
            return f(h, *a, **kw)
        finally:
            kinder = _STAPEL.pop()
            gesamt = h.dreiecke() - vorher
            _SUMME[name] = _SUMME.get(name, 0) + gesamt - kinder
            _AUFRUFE[name] = _AUFRUFE.get(name, 0) + 1
            if _STAPEL: _STAPEL[-1] += gesamt
    return g


def anlegen(*module):
    for m in module:
        for name, f in list(vars(m).items()):
            if not inspect.isfunction(f) or f.__module__ != m.__name__: continue
            ps = list(inspect.signature(f).parameters)
            if ps and ps[0] == "h": setattr(m, name, _verpacke(name, f))
    atexit.register(ausgeben)


def ausgeben():
    for n in sorted(_SUMME, key=lambda k: -_SUMME[k]):
        if _SUMME[n]: print(f"ZAEHLER {n} {_SUMME[n]} {_AUFRUFE[n]}")
