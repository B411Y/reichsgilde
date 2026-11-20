using Reichsgilde.Kartenbau;
using Reichsgilde.KartenbauMcp;

var ordner = Projektordner.Finden() ?? throw new InvalidOperationException("Projektordner nicht gefunden – im Ordner reichsgilde aufrufen.");
var arbeit = Path.Combine(Path.GetTempPath(), "kartenbau-mcp-test-" + Environment.ProcessId);
var w = new Werkzeuge(ordner, arbeit);
var fehler = 0;
var vorherSvg = File.GetLastWriteTimeUtc(Path.Combine(ordner.Vorschauen, "greybox.svg"));

void Pruefe(string name, Func<bool> test)
{
    bool ok;
    try { ok = test(); } catch (Exception ex) { Console.WriteLine("   Ausnahme: " + ex.Message); ok = false; }
    Console.WriteLine((ok ? "ok      " : "FEHLER  ") + name);
    if (!ok) fehler++;
}

bool Wirft(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }

Pruefe("karten_auflisten nennt greybox und Stadtskripte", () => w.KartenAuflisten() is var t && t.Contains("greybox") && t.Contains("Stadtskripte"));
Pruefe("karte_lesen: Greybox mit Größe, Zonen, Gebäude", () => w.KarteLesen("greybox") is var t && t.Contains("(greybox)") && t.Contains("Zonen:") && t.Contains("Gebäude:") && t.Contains("Wege:"));
Pruefe("karte_pruefen: Greybox ohne Fehler", () => w.KartePruefen("greybox") is var t && t.StartsWith("greybox: 0 Fehler"));
Pruefe("karte_bild: SVG im Arbeitsordner, nicht in docs/karten", () =>
{
    var (text, svg, _) = w.KarteBild("greybox");
    return svg is not null && svg.StartsWith("<svg") && File.Exists(Path.Combine(arbeit, "greybox.svg"))
           && File.GetLastWriteTimeUtc(Path.Combine(ordner.Vorschauen, "greybox.svg")) == vorherSvg && text.Contains("Bild von greybox");
});
Pruefe("karte_bauen ohne schreiben: nur Entwurf im Arbeitsordner", () =>
{
    var vorher = File.GetLastWriteTimeUtc(ordner.Karte("probestadt"));
    var t = w.KarteBauen("probestadt");
    return t.Contains("Entwurf geschrieben") && File.Exists(Path.Combine(arbeit, "probestadt.json")) && File.GetLastWriteTimeUtc(ordner.Karte("probestadt")) == vorher;
});
Pruefe("karte_bauen mit schreiben ohne ueberschreiben: Datei bleibt (Rückgabe 2)", () =>
{
    var vorher = File.GetLastWriteTimeUtc(ordner.Karte("probestadt"));
    var t = w.KarteBauen("probestadt", schreiben: true);
    return t.Contains("Rückgabe 2") && File.GetLastWriteTimeUtc(ordner.Karte("probestadt")) == vorher;
});
Pruefe("Entwurf lässt sich lesen und prüfen", () => w.KarteLesen("probestadt").Contains("(probestadt)"));
Pruefe("Ausbruch: Pfade und Sonderzeichen werden abgelehnt", () =>
    Wirft(() => w.KarteLesen("../../etc/passwd")) && Wirft(() => w.KartePruefen("..\\x")) && Wirft(() => w.KarteBild("/tmp/x"))
    && Wirft(() => w.KarteBauen("../Koeln", schreiben: true)) && Wirft(() => w.KarteLesen("")) && Wirft(() => w.KarteLesen("gibtesnicht")));

try { Directory.Delete(arbeit, true); } catch (IOException) { }
Console.WriteLine(fehler == 0 ? "Alle Tests grün." : $"{fehler} Test(s) fehlgeschlagen.");
return fehler == 0 ? 0 : 1;
