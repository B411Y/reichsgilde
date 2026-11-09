using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;
using Reichsgilde.Core.Zeit;

namespace Reichsgilde;

/// <summary>
/// Das Wasser der Karte – ein Teil der <c>partial class Stadtansicht</c> (Kartenwerkstatt, Schritt 5c; realistisches Wasser, Phase 3:
/// <c>docs/wasser/phase3.md</c>). Der Kern rechnet Flüsse, Bäche, Seen und Teiche samt Spiegel und Bett (<see cref="Wasserform"/>) und die
/// Übergänge darüber (<see cref="Wasserplan"/>); das Bett steckt schon im Boden (Stadtansicht.Gelaende.cs). Gezeichnet wird alles mit
/// <c>wasser_realistisch.gdshaderinc</c>; welche Gewässerart (<see cref="Wasserart"/>) ein Gewässer ist, entscheiden Breite bzw. Fläche aus der Karte
/// (<see cref="Wasserarten.AusKarte"/>), welche Stufe des Shaders und wie fein das Netz, die Grafik-Voreinstellung (<see cref="Wasserarten.StufeAusGrafik"/>).
/// <list type="bullet">
/// <item><b>Flüsse und Bäche</b> (<see cref="FlussNetze"/>): ein Band entlang der Mittellinie auf der Höhe des Spiegels (er fällt nur, steigt nie), etwas breiter
/// als der Fluss – die Ränder verschwinden im Ufer –, quer und längs in Ecken zu 2 m (Ultra 1 m) geteilt und in Stücke zu 64 m (Godot zeichnet nur die
/// Stücke im Bild). Wo der gezeichnete Boden das Bett nicht zeigt (ein schmaler Bach, eine Furt), liegt das Band knapp darüber (<see cref="Wasserdeckel"/>).
/// Die Ecken tragen Laufachsen und Strömung (die Wellen folgen den Kurven) und in <c>COLOR</c> Uferabstand, „offen“ (Eis bleibt zurück: an einer Furt
/// nur Randeis, Mündung, schnelle Stelle eines Baches und Fährrinne frieren nie), Schaumanteil (nur ab 30 m Breite; an einem Hafen nur ein schmaler Saum) und Deckkraft (in der Mündung übernimmt das größere Gewässer).</item>
/// <item><b>Seen und Teiche</b> (<see cref="SeeNetz"/>): eine Fläche genau über den Zellen des Bodens (<see cref="RasterDreiecke"/>) auf der Höhe des
/// Wasserstands, der Uferabstand jeder Ecke aus dem Umriss.</item>
/// <item><b>Hafenbecken</b> (<see cref="BeckenNetz"/>): vor der Kaimauer eines Hafens hebt der Kern das Ufer aus; darin liegt ruhiges Wasser ohne Schaum auf dem
/// Spiegel des Flusses davor und blendet dort, wo der Fluss beginnt, in ihn über.</item>
/// <item><b>Eis</b> (<see cref="WasserWinterNachfuehren"/>): Winterfortschritt aus Jahreszeit und Tageszeit des Kerns (ein Spieltag ist ein Jahr), Regeln je
/// Art (<see cref="Wasserarten"/>, Nutzer 30.10.2026): Teich, Hafenbecken und Graben frieren zuerst ganz zu, ein Bach später (offen bleiben ein Loch an der
/// Mündung und an schnellen Stellen, <see cref="Muendungen"/>, <see cref="SchnelleStellen"/>), an einer Furt nur Randeis, ein Fluss bekommt Randeis und
/// Schollen. <b>Wetter</b>: Wind, Regen, Schnee und Himmelsfarbe liest der Shader aus den globalen Werten (Stadtansicht.Wetter.cs).</item>
/// <item><b>Übergänge, Fähren, Hafen</b>: Stadtansicht.Wasser.Uebergaenge.cs.</item>
/// </list>
/// Im Spiel entsteht das Wasser einmal mit der Karte (<see cref="GewaesserBauen"/>), in der Kartenwerkstatt neu, sobald ein Befehl Gewässer, Wege, Fähren oder
/// Gelände geändert hat (Stadtansicht.Kartenwerkstatt.Wasser.cs); wechselt die Grafikstufe, nur die Wasserflächen (<see cref="WasserStufeNachfuehren"/>).
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Das Band eines Flusses ist um diesen Faktor breiter als der Fluss: Die Ränder liegen unter dem Ufer.</summary>
    private const float FlussRandFaktor = 1.15f;

    /// <summary>In diesem Abstand (Meter) tastet <see cref="Wasserdeckel"/> den Boden längs ab (der Kern gibt einen Punkt je Meter, das Raster hat 2 m).</summary>
    private const int FlussSchritt = 2;

    /// <summary>So lang (Meter) ist ein Stück des Flussbandes: Godot lässt Stücke außerhalb des Bildes weg.</summary>
    private const float FlussStueck = 64f;

    /// <summary>So hoch (Meter) liegt das Wasser mindestens über dem gezeichneten Boden (und dem Weg einer Furt) in der Mitte des Bandes.</summary>
    private const float WasserUeberBoden = 0.03f;

    /// <summary>Um eine Furt (ab der halben Wegbreite) bleibt in diesem Umkreis (m) nur Randeis; bis zur doppelten Weite geht es ins volle Eis über.</summary>
    private const float OffenUmkreis = 3f;

    /// <summary>Fährrinne: Der Fährmann hält quer durch das Randeis eine Rinne offen, so breit wie die Fähre (halbe Breite 2 m) und dieser Zugabe (m), weich bis zur doppelten.</summary>
    private const float FaehreHalb = 2f, RinneZugabe = 1f;

    /// <summary>Loch im Eis (Mündung, schnelle Stelle): offen im Halbmesser der Stelle, weich ins Eis über diese Meter.</summary>
    private const float LochRand = 3f;

    /// <summary>Schnelle Stelle eines Baches: Gefälle des Spiegels (über ±<see cref="GefaelleFenster"/> m) ab diesem Wert; je Stück die steilste Stelle, höchstens eine je <see cref="SchnellAbstand"/> m.</summary>
    private const float SchnellAbGefaelle = 0.035f, SchnellAbstand = 40f;
    private const int GefaelleFenster = 4;

    /// <summary>
    /// Grundspiegelung des Himmels (<c>himmel_grund</c>): Die Stadt hat keinen Himmel, den Godot im Wasser spiegeln könnte (nur eine Hintergrundfarbe), und die
    /// Kamera schaut steil von oben, wo die echte Spiegelung schwach ist. Ohne diesen Anteil wäre tiefes Wasser fast schwarz.
    /// </summary>
    private const float WasserHimmelGrund = 0.2f;

    /// <summary>Wiederholung der Normalkarten je Meter im Spiel (Testszene 0,25): Die Kamera ist weiter weg, kleinere Kräusel wirkten dort wie Rauschen.</summary>
    private const float WasserBildMassstab = 0.15f;

    private Node3D? _gewaesserKnoten;
    private Node3D? _wasserFlaechen;
    private readonly List<(ShaderMaterial Stoff, Wasserart Art)> _wasserStoffe = new();
    private Wasserstufe _wasserStufe;
    private float _wasserWinter = -1;

    /// <summary>Wo das Eis aller Gewässer ein Loch hat (Mündungen, schnelle Stellen der Bäche), beim Bau der Flächen gerechnet.</summary>
    private List<OffeneStelle> _wasserLoecher = new();

    /// <summary>Die Mündungen der Läufe in andere Gewässer (beim Bau der Flächen gerechnet).</summary>
    private List<Muendung> _muendungen = new();

    /// <summary>So viele Löcher bzw. Mündungen nimmt der Shader je Gewässer (<c>loch_ort[8]</c>, <c>muendung[4]</c>).</summary>
    private const int LoecherJeStoff = 8, MuendungenJeStoff = 4;

    /// <summary>
    /// Startoption <c>--winter=0..1</c>: Eis so weit gewachsen, unabhängig von der Uhr (für Bildschirmfotos). Eis gibt es nur im Winter: Über 0 zeigt
    /// die Ansicht ohne <c>--jahreszeit</c> den Winter (<see cref="GezeigteJahreszeit"/>); mit einer anderen vorgegebenen Jahreszeit bleibt das Wasser offen.
    /// </summary>
    private static readonly float? WinterVorgabe =
        float.TryParse(Wasserarten.Startoption("winter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w)
            ? Mathf.Clamp(w, 0f, 1f) : null;

    /// <summary>Startoption <c>--tauwetter=0..1</c>: Eisgang so weit (siehe <see cref="Wasserarten.Tauwetter"/>); ohne <c>--winter</c> mit vollem Eis.</summary>
    private static readonly float? TauwetterVorgabe =
        float.TryParse(Wasserarten.Startoption("tauwetter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var tw)
            ? Mathf.Clamp(tw, 0f, 1f) : null;

    /// <summary>Ob die Startoptionen den Winter zeigen sollen (<c>--winter</c> über 0 oder <c>--tauwetter</c>), wenn keine Jahreszeit vorgegeben ist.</summary>
    private static bool WinterAusStartoption => WinterVorgabe > 0f || TauwetterVorgabe is not null;

    private float _wasserTauen;

    /// <summary>Das Wasser, das die Ansicht zeichnet: im Spiel das der Karte, in der Werkstatt das des Entwurfs (folgt jeder Änderung von selbst).</summary>
    private Wasserform? GewaesserForm => _gelaende?.Wasser;

    /// <summary>Brücken, Furten und Fähren der Karte bzw. des Entwurfs (null ohne Karte).</summary>
    private Wasserplan? Uebergangsplan => _entwurf?.Wasserplan ?? _karte?.Wasserplan;

    /// <summary>
    /// Ein Bau der Wasserflächen (in Häppchen, <see cref="WasserflaechenSchritte"/>): Was er baut, hängt noch nirgends – erst <see cref="WasserflaechenAnwenden"/>
    /// tauscht es gegen die alten Flächen, bis dahin bleibt das Bild, wie es war.
    /// </summary>
    private sealed class WasserLauf
    {
        public Node3D? Flaechen;
        public readonly List<(ShaderMaterial Stoff, Wasserart Art)> Stoffe = new();
        public readonly List<(Wasserlauf Lauf, Wasserart Art, ShaderMaterial Stoff)> Schollen = new();
        public IEnumerator<int>? Schritte;

        /// <summary>Was die Flächen vom Wasserplan lesen (Furten, Fähren) beim Start des Baus (Kartenwerkstatt.Wasser.cs).</summary>
        public string FlaechenZeichen = "";
        public bool NurUebergaenge, WegeNeu;
    }

    /// <summary>
    /// Baut Wasserflächen, Brücken, Furten, Fähren neu auf (oder zum ersten Mal), in einem Zug. Eine Karte ohne Wasser bekommt nichts und kostet nichts.
    /// Die Werkstatt baut es in Häppchen (Stadtansicht.Kartenwerkstatt.Wasser.cs).
    /// </summary>
    private void GewaesserBauen()
    {
        var bau = new WasserLauf { FlaechenZeichen = _entwurf is null ? "" : UebergangsZeichen().Flaechen };
        if (GewaesserForm is { IstLeer: false } form)
        {
            var uhr = Pinselmessung.Uhr();
            foreach (var _ in WasserflaechenSchritte(form, bau)) { }
            Pinselmessung.Dazu("    Wasser: Flächen", uhr);
        }
        GewaesserTauschen(bau);
    }

    /// <summary>Das neue Wasser gilt: die alten Knoten gehen, die neuen Flächen und die Übergänge aus dem Wasserplan kommen an ihre Stelle.</summary>
    private void GewaesserTauschen(WasserLauf bau)
    {
        if (_gewaesserKnoten is not null) KnotenEntfernen(_gewaesserKnoten);
        _gewaesserKnoten = null;
        _wasserFlaechen = null;
        _wasserStoffe.Clear();
        UebergaengeVergessen();
        _wsUebergangsKnoten.Clear();
        if (bau.Flaechen is null) return;
        _gewaesserKnoten = new Node3D { Name = "Gewaesser" };
        AddChild(_gewaesserKnoten);
        WasserflaechenAnwenden(bau);
        _wsFlaechenZeichen = bau.FlaechenZeichen;
        UebergaengeNeuBauen();
    }

    /// <summary>Die Brücken, Furten und Fähren aus dem Wasserplan unter <see cref="_gewaesserKnoten"/> (ihre Knoten stehen danach in <see cref="_wsUebergangsKnoten"/>).</summary>
    private void UebergaengeNeuBauen()
    {
        if (_gewaesserKnoten is null) return;
        var uhr = Pinselmessung.Uhr();
        var erster = _gewaesserKnoten.GetChildCount();
        if (Uebergangsplan is { } plan) UebergaengeBauen(_gewaesserKnoten, plan);
        _wsUebergangsKnoten.Clear();
        for (var i = erster; i < _gewaesserKnoten.GetChildCount(); i++) _wsUebergangsKnoten.Add(_gewaesserKnoten.GetChild(i));
        if (_entwurf is not null) _wsUebergangsZeichen = UebergangsZeichen().Uebergaenge;
        Pinselmessung.Dazu("    Wasser: Übergänge", uhr);
    }

    /// <summary>Die Knoten der Übergänge unter <see cref="_gewaesserKnoten"/> (zum Ersetzen, ohne die Flächen anzufassen).</summary>
    private readonly List<Node> _wsUebergangsKnoten = new();

    /// <summary>Was der Wasserplan beim letzten Bau gesagt hat (Kartenwerkstatt.Wasser.cs): alle Übergänge, und was die Flächen davon lesen.</summary>
    private string? _wsUebergangsZeichen, _wsFlaechenZeichen;

    /// <summary>Die Wasserflächen (ohne Übergänge) in der Stufe der Grafik-Voreinstellung, mit einem Stoff je Gewässer; sie ersetzen die alten.</summary>
    private void WasserflaechenBauen(Wasserform form)
    {
        if (_gewaesserKnoten is null) return;
        var bau = new WasserLauf();
        foreach (var _ in WasserflaechenSchritte(form, bau)) { }
        WasserflaechenAnwenden(bau);
    }

    /// <summary>Die gebauten Flächen ersetzen die alten (samt Stoffen und wartenden Schollenfeldern).</summary>
    private void WasserflaechenAnwenden(WasserLauf bau)
    {
        if (_gewaesserKnoten is null || bau.Flaechen is null) return;
        if (_wasserFlaechen is not null) KnotenEntfernen(_wasserFlaechen);
        _wasserFlaechen = bau.Flaechen;
        _gewaesserKnoten.AddChild(_wasserFlaechen);
        _wasserStoffe.Clear();
        _wasserStoffe.AddRange(bau.Stoffe);
        SchollenVergessen();
        _schollenWarten.AddRange(bau.Schollen);
    }

    /// <summary>
    /// Baut die Wasserflächen in Häppchen (ein Stück Fluss von <see cref="FlussStueck"/> Metern je Schritt) in <paramref name="bau"/>; nichts davon ist im Bild,
    /// bis <see cref="WasserflaechenAnwenden"/> es einsetzt. Dieselben Flächen wie der Bau in einem Zug.
    /// </summary>
    private IEnumerable<int> WasserflaechenSchritte(Wasserform form, WasserLauf bau)
    {
        var flaechen = bau.Flaechen = new Node3D { Name = "Wasserflaechen" };
        _wasserStufe = _wasserStufeMessung ?? Wasserarten.StufeAusGrafik();
        var shader = Wasserarten.ShaderFuer(_wasserStufe);
        var abstand = Wasserarten.NetzAbstand(_wasserStufe);
        (_wasserWinter, _wasserTauen) = WinterJetzt();
        GD.Print($"[wasser] Eis beim Aufbau: Winter {_wasserWinter:0.000}, Tauwetter {_wasserTauen:0.000} " +
                 $"(gezeigt {GezeigteJahreszeit(_simulation?.Zeit.Jahreszeit ?? Jahreszeit.Fruehling)}, Uhr {_simulation?.Zeit.Jahreszeit.ToString() ?? "keine Partie"})");
        var arten = form.Laeufe.ToDictionary(l => l, l => Wasserarten.AusKarte(l.Art, l.Punkte.Count > 0 ? l.Punkte.Max(p => p.Breite) : 0, 0, l.Stroemung));
        _muendungen = Muendungen(form);
        _wasserLoecher = _muendungen.Select(m => new OffeneStelle(m.Ort, m.Ort, Mathf.Max(1f, m.Halb), Offenart.Loch, "Mündung " + m.Bach.Name)).ToList();
        foreach (var (lauf, art) in arten)
            if (art.Name == Wasserarten.Bach.Name) _wasserLoecher.AddRange(SchnelleStellen(lauf));
        yield return 0;

        foreach (var lauf in form.Laeufe)
        {
            if (lauf.Punkte.Count < 2) continue;
            var art = arten[lauf];
            var (anfang, ende) = (lauf.Punkte[0].Ort, lauf.Punkte[^1].Ort);
            var richtung = new Vector2((float)(ende.X - anfang.X), (float)(ende.Z - anfang.Z));
            // Reihenfolge der Durchsichtigen: Flüsse zuerst, Bäche darüber (sie blenden in der Mündung in den Fluss über)
            var stoff = WasserStoff(shader, art, richtung, lauf: true, (float)lauf.Stroemung, abstand, art.Name == Wasserarten.Fluss.Name ? -8 : -7,
                Wasserarten.Saat(lauf.Name), bau.Stoffe);
            LoecherSetzen(stoff, lauf, null);
            MuendungenSetzen(stoff, lauf);
            var nr = 0;
            var netze = new List<ArrayMesh>();
            foreach (var _ in FlussNetze(lauf, form, abstand, art, netze))
            {
                while (nr < netze.Count) flaechen.AddChild(WasserInstanz($"Fluss_{lauf.Name}_{nr}", netze[nr++], stoff));
                yield return 0;
            }
            while (nr < netze.Count) flaechen.AddChild(WasserInstanz($"Fluss_{lauf.Name}_{nr}", netze[nr++], stoff));
            if (art.Schollen > 0) bau.Schollen.Add((lauf, art, stoff));
        }
        foreach (var see in form.Seen)
        {
            var art = Wasserarten.AusKarte(see.Art, 0, Flaecheninhalt(see.Umriss));
            if (SeeNetz(see, form, abstand, art) is not { } netz) continue;
            // Ein See im Wasserverbund (VerbundNetz): Uferabstand mit Vorzeichen im Netz, und vor dem Band des Laufs gezeichnet (das liegt darüber)
            var verbund = form.Verbuende.Any(v => ReferenceEquals(v.See, see));
            var stoff = WasserStoff(shader, art, new Vector2(1, 0.4f), lauf: false, null, abstand, verbund ? -9 : -8, Wasserarten.Saat(see.Name), bau.Stoffe);
            if (verbund) stoff.SetShaderParameter("ufer_vorzeichen", 1f);
            LoecherSetzen(stoff, null, see);
            flaechen.AddChild(WasserInstanz("See_" + see.Name, netz, stoff));
            yield return 0;
        }
        if (form.Becken.Count > 0)
        {
            var stoff = WasserStoff(shader, Wasserarten.Hafenbecken, new Vector2(1, 0.4f), lauf: false, null, abstand, -6, 0f, bau.Stoffe); // nach dem Fluss: blendet in ihn über
            foreach (var becken in form.Becken)
            {
                if (BeckenNetz(becken, form, abstand) is { } netz)
                    flaechen.AddChild(WasserInstanz("Becken_" + becken.Name, netz, stoff));
                yield return 0;
            }
        }
        foreach (var v in form.Verbuende)
            GD.Print($"[wasser] Verbund {v.Lauf.Name}–{v.See.Name}: Stellen {string.Join(", ", v.Stuecke.Select(st => $"{st.Von:0}–{st.Bis:0} m"))}, " +
                     $"ΔSpiegel {v.SpiegelUnterschied:0.00} m, Wasserstand {v.See.Wasserstand:0.00}{(v.See.StandAutomatisch ? " (vom Lauf übernommen)" : " (fest)")}");
        // Für Klicktests: was gebaut ist, wo die Furten (nur Randeis) und die Löcher im Eis (Mündung, schnelle Stelle) liegen
        var furten = Uebergangsplan?.Querungen.Where(q => q.Art == Uebergangsart.Furt).Select(q => $"{q.GewaesserName} bei {q.Mitte.X:0},{q.Mitte.Z:0}") ?? Enumerable.Empty<string>();
        GD.Print($"[wasser] Stufe {_wasserStufe}, Netz {abstand:0} m, {flaechen.GetChildCount()} Flächen, " +
                 $"{string.Join(", ", bau.Stoffe.Select(s => s.Art.Name).Distinct())}; Furten: {string.Join("; ", furten)}; " +
                 $"Löcher im Eis: {string.Join("; ", _wasserLoecher.Select(l => $"{l.Name} bei {l.A.X:0},{l.A.Y:0}"))}; " +
                 $"Hafenbecken: {string.Join("; ", form.Becken.Select(b => $"{b.Name} {b.Bereich.X0:0},{b.Bereich.Z0:0} bis {b.Bereich.X1:0},{b.Bereich.Z1:0}"))}; " +
                 $"Mündungen: {string.Join("; ", _muendungen.Select(m => $"{m.Bach.Name} in {m.Ziel?.Name ?? m.ZielSee?.Name ?? "?"} bei {m.Ort.X:0},{m.Ort.Y:0}" +
                     (m.Ziel is { } z && LaufStelle(z, m.Ort) is { } st ? $" (quer {st.Quer:0.0}, Stelle {st.Stelle:0}, halbe Breite {st.Halb:0.0}, " +
                         $"Spiegel {st.Spiegel:0.00}, Bach {LaufStelle(m.Bach, m.Ort)?.Spiegel:0.00})" : "")))}");
    }

    /// <summary>
    /// Was der Wasserplan über Brücken, Furten und Fähren sagt, als Text: <c>Uebergaenge</c> alles (Brücken, Furten, Fähren), <c>Flaechen</c> nur, was die Wasserflächen
    /// davon lesen (Furten und Fähren: das Eis bleibt dort offen, das Wasser steht über dem Weg). Gleicher Text heißt: nichts neu zu bauen (die Werkstatt baut das Wasser
    /// nach dem Anlegen eines Weges nur, wenn sich etwas geändert hat).
    /// </summary>
    private (string Uebergaenge, string Flaechen) UebergangsZeichen()
    {
        var plan = Uebergangsplan;
        if (plan is null) return ("", "");
        static string Z(Kartenort o) => $"{o.X:R},{o.Z:R}";
        var alle = new System.Text.StringBuilder();
        var flaechen = new System.Text.StringBuilder();
        foreach (var q in plan.Querungen)
        {
            var zeile = $"{q.Art}|{q.Weg}|{q.WegName}|{q.Gewaesser}|{q.GewaesserName}|{Z(q.Anfang)}|{Z(q.Ende)}|{q.VonStelle:R}|{q.BisStelle:R}|{Z(q.UferA)}|{Z(q.UferB)}|"
                        + $"{q.UferAStelle:R}|{q.UferBStelle:R}|{q.Spannweite:R}|{q.Breite:R}|{q.Richtung:R}|{q.Uferhoehe:R}|{q.Deckhoehe:R}|{q.AnfangHoehe:R}|{q.EndeHoehe:R}|"
                        + $"{q.Spiegel:R}|{q.Tiefe:R}|{q.Felder}|{q.Feldlaenge:R}|{q.Quert}|{q.Karren}|{string.Join(";", q.Pfeiler.Select(f => $"{Z(f.Ort)}@{f.Fuss:R}"))}\n";
            alle.Append(zeile);
            if (q.Art == Uebergangsart.Furt) flaechen.Append(zeile);
        }
        foreach (var f in plan.Faehren)
        {
            var zeile = $"F|{f.Nummer}|{f.Name}|{Z(f.Von)}|{Z(f.Bis)}|{f.Gewaesser}|{f.Laenge:R}|{f.UeberWasser:R}|{f.Spiegel:R}|{f.Richtung:R}|{f.VonImWasser}|{f.BisImWasser}|"
                        + $"{Z(f.VonUferkante)}|{Z(f.BisUferkante)}|{f.VonUfer:R}|{f.BisUfer:R}\n";
            alle.Append(zeile);
            flaechen.Append(zeile);
        }
        return (alle.ToString(), flaechen.ToString());
    }

    private ShaderMaterial WasserStoff(Shader shader, Wasserart art, Vector2 richtung, bool lauf, float? tempo, float abstand, int reihenfolge, float saat = 0f,
        List<(ShaderMaterial Stoff, Wasserart Art)>? ziel = null)
    {
        var stoff = new ShaderMaterial { Shader = shader, ResourceName = "Wasser_" + art.Name, RenderPriority = reihenfolge };
        Wasserarten.BilderSetzen(stoff);
        Wasserarten.Anwenden(stoff, art, richtung, _wasserWinter, uferImNetz: true, lauf, tempo, abstand, saat, _wasserTauen);
        stoff.SetShaderParameter("himmel_grund", WasserHimmelGrund);
        stoff.SetShaderParameter("sampler_scale", new Vector2(WasserBildMassstab, WasserBildMassstab));
        (ziel ?? _wasserStoffe).Add((stoff, art));
        return stoff;
    }

    private static MeshInstance3D WasserInstanz(string name, ArrayMesh netz, ShaderMaterial stoff) => new()
    {
        Name = name, Mesh = netz, MaterialOverride = stoff, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    /// <summary>Bei jeder Änderung der Grafik-Einstellungen (Stadtansicht.Grafik.cs): Hat sich die Stufe des Wassers geändert, die Flächen neu bauen.</summary>
    private void WasserStufeNachfuehren()
    {
        if (_gewaesserKnoten is null || GewaesserForm is not { IstLeer: false } form || _wasserStufeMessung is not null
            || Wasserarten.StufeAusGrafik() == _wasserStufe) return;
        WasserflaechenBauen(form);
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Eis nach Jahreszeit
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Winterfortschritt und Tauwetter jetzt: aus Jahreszeit und Fortschritt im Spieljahr (Kern: <see cref="Spielzeit.Jahreszeit"/>,
    /// <see cref="Spielzeit.MinuteImTag"/> mit der angebrochenen Minute), siehe <see cref="Wasserarten.Winterfortschritt"/> und
    /// <see cref="Wasserarten.Tauwetter"/>. Eis gibt es nur, wenn die Ansicht den Winter zeigt. Ist die Jahreszeit vorgegeben (<c>--jahreszeit</c>,
    /// Entwicklerfenster) und weicht von der Uhr ab, steht das Eis im Winter voll (ohne Eisgang), sonst gar nicht; <c>--winter</c> und
    /// <c>--tauwetter</c> setzen die Werte fest (nur im gezeigten Winter).
    /// </summary>
    private (float Winter, float Tauen) WinterJetzt()
    {
        var uhr = _simulation?.Zeit.Jahreszeit;
        var gezeigt = GezeigteJahreszeit(uhr ?? Jahreszeit.Fruehling);
        if (gezeigt != Jahreszeit.Winter) return (0f, 0f);
        if (WinterVorgabe is not null || TauwetterVorgabe is not null) return (WinterVorgabe ?? 1f, TauwetterVorgabe ?? 0f);
        if (_simulation is null || uhr != Jahreszeit.Winter) return (1f, 0f);
        var zeit = _simulation.Zeit;
        var anteil = (zeit.MinuteImTag + _simulation.Takt.AngebrocheneMinute) / Spielzeit.MinutenProTag;
        return (Wasserarten.Winterfortschritt(zeit.Jahreszeit, anteil), Wasserarten.Tauwetter(zeit.Jahreszeit, anteil));
    }

    private double _eisMeldung = -1;

    /// <summary>
    /// Jedes Bild (aus <see cref="WasserBild"/>): das Eis aller Gewässer auf den Winterfortschritt, je Art nach <see cref="Wasserarten.EisFortschritt"/>,
    /// Eisgang (<c>eis_tauen</c>) und die Wintertönung des offenen Wassers (nur bei Änderung). Im Übergang alle 20 s eine Zeile ins Protokoll.
    /// </summary>
    private void WasserWinterNachfuehren()
    {
        if (_wasserStoffe.Count == 0) return;
        var (winter, tauen) = WinterJetzt();
        if (Mathf.Abs(winter - _wasserWinter) < 0.002f && Mathf.Abs(tauen - _wasserTauen) < 0.002f) return;
        var sprung = Mathf.Abs(winter - _wasserWinter) > 0.2f || Mathf.Abs(tauen - _wasserTauen) > 0.2f;
        (_wasserWinter, _wasserTauen) = (winter, tauen);
        foreach (var (stoff, art) in _wasserStoffe)
        {
            stoff.SetShaderParameter("eis", Wasserarten.EisFortschritt(art, winter));
            stoff.SetShaderParameter("eis_still", Wasserarten.EisFortschritt(Wasserarten.Hafenbecken, winter));
            stoff.SetShaderParameter("winter_ton", Wasserarten.WinterTon(winter, tauen));
            stoff.SetShaderParameter("eis_tauen", tauen);
        }
        var jetzt = Time.GetTicksMsec() / 1000.0;
        if (sprung || jetzt - _eisMeldung >= 20)
        {
            _eisMeldung = jetzt;
            GD.Print($"[wasser] Eis: Winter {winter:0.000}, Tauwetter {tauen:0.000} ({GezeigteJahreszeit(_simulation?.Zeit.Jahreszeit ?? Jahreszeit.Fruehling)}" +
                     (_simulation is { } sim ? $", {sim.Zeit.Jahreszeit} {sim.Zeit.MinuteImTag / (double)Spielzeit.MinutenProTag:0.000} des Jahres" : "") + ")");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Flüsse und Bäche
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Das Band eines Flusses in Stücken (jedes fertige Stück kommt in <paramref name="netze"/>, danach ein Schritt): Reihen alle <paramref name="abstand"/> Meter entlang der Mittellinie, in jeder Reihe gleich viele Ecken quer
    /// (Abstand höchstens <paramref name="abstand"/>, mindestens vier Felder), alle auf der Höhe des Spiegels. UV = (quer, Stelle) in Metern, UV2 = Strömung
    /// nach x und z, COLOR = (Uferabstand / 32, Eiskanal (<see cref="Wasserarten.EisKanal"/>: offen bzw. still vor einer Kaimauer), Schaumanteil, Deckkraft) –
    /// wie <c>wasser_realistisch.gdshaderinc</c> sie liest.
    /// <para>Kurven (Befund 3, 09.11.2026): Die Richtung einer Reihe ist geglättet (<see cref="GeglaetteteRichtungen"/>), sonst gaben Knicke des Polygonzugs
    /// örtlich winzige Halbmesser. Wo eine Kurve enger ist als das halbe Band (Ecke · Krümmung ≥ 1), fielen die Reihen auf der Innenseite übereinander
    /// (ein Fächer): Dort rückt eine Ecke höchstens bis auf <see cref="KurveKlemme"/> des Halbmessers an den Krümmungsmittelpunkt heran, und eine
    /// Kehlenkappe um den Mittelpunkt schließt das Loch (<see cref="Kehlenkappe"/>). Der Uferabstand ist der wahre Abstand der Ecke zur Mitte – ohne
    /// Faltung liegt jede Ecke ihrer Reihe am nächsten, so passt er zum Kern.</para>
    /// </summary>
    private IEnumerable<int> FlussNetze(Wasserlauf lauf, Wasserform form, float abstand, Wasserart art, List<ArrayMesh> netze)
    {
        var p = lauf.Punkte;
        var schritt = Math.Max(1, Mathf.RoundToInt(abstand));
        var reihen = new List<int>();
        for (var i = 0; i < p.Count; i += schritt) reihen.Add(i);
        if (reihen[^1] != p.Count - 1) reihen.Add(p.Count - 1);
        var groessteHalbe = (float)p.Max(w => w.Breite) / 2 * FlussRandFaktor;
        var felder = Math.Max(4, Mathf.CeilToInt(2 * groessteHalbe / abstand));
        if (felder % 2 == 1) felder++; // eine Ecke genau in der Mitte (dort ist das Ufer am weitesten)
        var andere = AndereGewaesser(form, lauf, null);
        var offenStellen = OffeneStellen(form, lauf.Nummer, lauf.Name);
        var tempo = (float)lauf.Stroemung;
        // Läufe, in die dieser mündet: Dort blendet er weich über (statt an der ersten Ecke drinnen hart abzubrechen)
        var muendetIn = _muendungen.Where(m => m.Bach == lauf && m.Ziel is not null).Select(m => m.Ziel!).Distinct().ToList();
        var richtungen = GeglaetteteRichtungen(p, schritt);

        var ecken = new List<Vector3>();
        var uv = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var farben = new List<Color>();
        var stueckAnfang = 0; // erste Reihe des Stücks

        // Eine Ecke: Höhe, Laufachsen, Strömung und Netzwerte am Ort; o = Abstand zur Mitte (quer, mit Vorzeichen; sein Betrag gibt den Uferabstand)
        void Ecke(Vector2 ort, float o, float stelle, Vector2 richtung, float halb, float hoehe, float schaum, float furtOffen)
        {
            // In der Mündung: Liegt der Bach höher als der Fluss, fällt seine Fläche über die letzten Meter auf dessen Spiegel (statt als Platte über
            // dem Fluss zu enden) und blendet vom Rand des Flusses 1 m davor bis 3 m hinein weich aus – das Bachwasser läuft sichtbar hinein
            var muendung = muendetIn.Count > 0 ? EinMuendung(muendetIn, ort) : null;
            var eckHoehe = hoehe;
            if (muendung is { } mu && hoehe > mu.Spiegel + 0.02f)
                eckHoehe = Mathf.Lerp(hoehe, mu.Spiegel + 0.02f, Mathf.SmoothStep(-4f, 1.5f, mu.Hinein));
            ecken.Add(new Vector3(ort.X, eckHoehe, ort.Y));
            uv.Add(new Vector2(o, stelle));
            uv2.Add(richtung * tempo);
            var (offen, kai) = Offenheit(offenStellen, ort, furtOffen);
            var fremd = Fremdanteil(form, andere, ort, lauf, null);
            var deckkraft = 1f - fremd;
            if (muendung is { } m2) deckkraft = 1f - Mathf.SmoothStep(-1f, 3f, m2.Hinein);
            // Vor einer Kaimauer ist das Wasser still und friert wie das Hafenbecken (das Becken selbst ist in Köln schmal und liegt unter den Kähnen)
            var ufer = form.Verbuende.Count > 0 ? BandUfer(form, lauf, ort, halb - Mathf.Abs(o), eckHoehe - BodenHoehe(ort.X, ort.Y)) : halb - Mathf.Abs(o);
            farben.Add(new Color(Mathf.Clamp(ufer / 32f, 0f, 1f), Wasserarten.EisKanal(Mathf.Lerp(offen, 1f, fremd), kai),
                schaum * Mathf.Lerp(1f, Wasserarten.KaiSchaumAnteil, kai), deckkraft));
        }

        // Geklemmte Reihen (für die Kehlenkappen) und je Folge eine Zeile im Protokoll
        var kappen = new List<(Vector2 Mittelpunkt, float Halbmesser, float Radius, float Vorzeichen, float Stelle, Vector2 Richtung, Vector2 Mitte, float Halb,
            float Hoehe, float Schaum, float FurtOffen)>();
        var (folgeAb, engsteR) = (-1f, float.MaxValue);
        // Prüfhilfe: Ecken am äußeren Rand des Bandes, die sichtbar sind (Uferabstand > 0 und Deckkraft über 5 %) – dort endet das Band mit einer harten Kante
        var (randSichtbar, randBeispiel) = (0, Vector2.Zero);

        for (var r = 0; r < reihen.Count; r++)
        {
            var i = reihen[r];
            var pt = p[i];
            var richtung = richtungen[i];
            var quer = new Vector2(-richtung.Y, richtung.X);
            var halb = (float)pt.Breite / 2;
            var mitte = new Vector2((float)pt.Ort.X, (float)pt.Ort.Z);
            var hoehe = Mathf.Max((float)pt.Spiegel, Wasserdeckel(mitte, richtung, halb));
            var schaum = Mathf.SmoothStep(Wasserarten.SchaumAbBreite - 4f, Wasserarten.SchaumAbBreite, (float)pt.Breite);
            var furtOffen = Wasserarten.FurtOffen(art, halb);
            // Krümmung (1/m, + = die Kurve biegt zur Seite von quer, dort liegt der Mittelpunkt) aus den geglätteten Richtungen der Nachbarreihen
            var (iv, inach) = (reihen[Math.Max(0, r - 1)], reihen[Math.Min(reihen.Count - 1, r + 1)]);
            var weg = (float)(p[inach].Stelle - p[iv].Stelle);
            var kappa = weg > 1e-3f ? Mathf.Atan2(richtungen[iv].Cross(richtungen[inach]), richtungen[iv].Dot(richtungen[inach])) / weg : 0f;
            var geklemmt = false;
            for (var j = 0; j <= felder; j++)
            {
                var o = halb * FlussRandFaktor * (2f * j / felder - 1f);
                if (o * kappa > KurveKlemme) // Innenseite einer zu engen Kurve: nicht über den Mittelpunkt hinaus (sonst fallen die Reihen übereinander)
                {
                    o = KurveKlemme / kappa;
                    geklemmt = true;
                }
                Ecke(mitte + quer * o, o, (float)pt.Stelle, richtung, halb, hoehe, schaum, furtOffen);
                if ((j == 0 || j == felder) && farben[^1].R > 0.0005f && farben[^1].A > 0.05f)
                    (randSichtbar, randBeispiel) = (randSichtbar + 1, randSichtbar == 0 ? mitte + quer * o : randBeispiel);
            }
            if (geklemmt)
            {
                var halbmesser = 1f / Mathf.Abs(kappa);
                kappen.Add((mitte + quer / kappa, halbmesser, halb * FlussRandFaktor - KurveKlemme * halbmesser + 0.3f, Mathf.Sign(kappa), (float)pt.Stelle,
                    richtung, mitte, halb, hoehe, schaum, furtOffen));
                if (folgeAb < 0) folgeAb = (float)pt.Stelle;
                engsteR = Mathf.Min(engsteR, halbmesser);
            }
            if (folgeAb >= 0 && (!geklemmt || r == reihen.Count - 1))
            {
                GD.Print($"[wasser] Kurve {lauf.Name} bei {folgeAb:0} m: R={engsteR:0.0} m bei halber Breite {halb:0.0} m, geklemmt");
                (folgeAb, engsteR) = (-1f, float.MaxValue);
            }
            // Stück abschließen: nach FlussStueck Metern oder am Ende (die letzte Reihe ist zugleich die erste des nächsten Stücks)
            var laenge = (float)(pt.Stelle - p[reihen[stueckAnfang]].Stelle);
            if (r > stueckAnfang && (laenge >= FlussStueck || r == reihen.Count - 1))
            {
                netze.Add(BandNetz(ecken, uv, uv2, farben, felder));
                yield return r; // ein Stück je Schritt: die Werkstatt baut das Wasser in Häppchen (Stadtansicht.Kartenwerkstatt.Wasser.cs)
                if (r == reihen.Count - 1) break;
                var letzte = felder + 1;
                ecken = ecken.GetRange(ecken.Count - letzte, letzte);
                uv = uv.GetRange(uv.Count - letzte, letzte);
                uv2 = uv2.GetRange(uv2.Count - letzte, letzte);
                farben = farben.GetRange(farben.Count - letzte, letzte);
                stueckAnfang = r;
            }
        }

        if (randSichtbar > 0)
            GD.Print($"[wasser] {lauf.Name}: {randSichtbar} Ecken am äußeren Rand des Bandes sichtbar (harte Kante), z. B. bei {randBeispiel.X:0.0},{randBeispiel.Y:0.0}");

        // Die Kehlenkappen: je geklemmte Reihe eine kleine Scheibe um den Krümmungsmittelpunkt, auf der Höhe der Reihe, mit denselben Netzwerten. Der Abstand
        // eines Punktes der Scheibe zur Mitte des Laufs ist Halbmesser − Abstand zum Mittelpunkt; die Strömung wie im Band (sonst stünde die Scheibe als
        // ruhender Fleck im fließenden Wasser – die Normalkarten liegen in Weltachsen, gleiche Strömung heißt gleiches Bild)
        if (kappen.Count > 0)
        {
            (ecken, uv, uv2, farben) = (new List<Vector3>(), new List<Vector2>(), new List<Vector2>(), new List<Color>());
            var indizes = new List<int>();
            foreach (var k in kappen)
                Kehlenkappe(k.Mittelpunkt, k.Radius, indizes, ecken, ort =>
                {
                    var o = k.Vorzeichen * (k.Halbmesser - ort.DistanceTo(k.Mittelpunkt));
                    Ecke(ort, o, k.Stelle + (ort - k.Mitte).Dot(k.Richtung), k.Richtung, k.Halb, k.Hoehe, k.Schaum, k.FurtOffen);
                });
            netze.Add(NetzAusFeldern(ecken, uv, uv2, farben, indizes));
            yield return 0;
        }
    }

    /// <summary>
    /// So nah (Anteil des Halbmessers) rückt eine Ecke auf der Innenseite einer Kurve höchstens an den Krümmungsmittelpunkt heran (Vorgabe Teamleiter
    /// 09.11.2026): näher, und die Reihen fielen übereinander.
    /// </summary>
    private const float KurveKlemme = 0.85f;

    /// <summary>So viele Dreiecke hat eine Kehlenkappe (ein Fächer um den Mittelpunkt).</summary>
    private const int KappeTeile = 10;

    /// <summary>
    /// Die Fließrichtung je Punkt eines Laufs (Befund 3, 09.11.2026): erst als Sehne über ± max(2, <paramref name="schritt"/>) Punkte (statt ± 1 m – Knicke des
    /// Polygonzugs gaben sonst örtlich winzige Halbmesser), dann gleitend gemittelt über ein Fenster von etwa der halben Breite. Dieselbe Richtung geht in die
    /// Strömung (UV2).
    /// </summary>
    private static Vector2[] GeglaetteteRichtungen(IReadOnlyList<Wasserpunkt> p, int schritt)
    {
        var n = p.Count;
        var k = Math.Max(2, schritt);
        var roh = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var (a, b) = (p[Math.Max(0, i - k)].Ort, p[Math.Min(n - 1, i + k)].Ort);
            var d = new Vector2((float)(b.X - a.X), (float)(b.Z - a.Z));
            roh[i] = d.LengthSquared() > 1e-8f ? d.Normalized() : new Vector2((float)p[i].RichtungX, (float)p[i].RichtungZ);
        }
        // gleitendes Mittel über Präfixsummen (die Punkte liegen etwa 1 m auseinander: das Fenster in Punkten ist die halbe Breite in Metern)
        var summe = new Vector2[n + 1];
        for (var i = 0; i < n; i++) summe[i + 1] = summe[i] + roh[i];
        var aus = new Vector2[n];
        for (var i = 0; i < n; i++)
        {
            var w = Math.Max(1, Mathf.RoundToInt((float)p[i].Breite / 2));
            var (von, bis) = (Math.Max(0, i - w), Math.Min(n - 1, i + w));
            var m = summe[bis + 1] - summe[von];
            aus[i] = m.LengthSquared() > 1e-8f ? m.Normalized() : roh[i];
        }
        return aus;
    }

    /// <summary>Eine Kehlenkappe: ein Fächer aus <see cref="KappeTeile"/> Dreiecken um <paramref name="mittelpunkt"/> mit dem Halbmesser <paramref name="radius"/>; <paramref name="ecke"/> legt jede Ecke an.</summary>
    private static void Kehlenkappe(Vector2 mittelpunkt, float radius, List<int> indizes, List<Vector3> ecken, Action<Vector2> ecke)
    {
        var basis = ecken.Count;
        ecke(mittelpunkt);
        for (var t = 0; t < KappeTeile; t++)
        {
            var winkel = Mathf.Tau * t / KappeTeile;
            ecke(mittelpunkt + new Vector2(Mathf.Cos(winkel), Mathf.Sin(winkel)) * radius);
        }
        for (var t = 0; t < KappeTeile; t++) DreieckNachOben(indizes, ecken, basis, basis + 1 + t, basis + 1 + (t + 1) % KappeTeile);
    }

    /// <summary>
    /// Wie weit (m) ein Ort im Inneren eines der Läufe liegt, in die dieser mündet (positiv: hinter dessen Rand), und dessen Spiegel dort; null weit weg.
    /// </summary>
    private static (float Hinein, float Spiegel)? EinMuendung(List<Wasserlauf> ziele, Vector2 ort)
    {
        (float, float)? tiefste = null;
        foreach (var ziel in ziele)
        {
            if (!ziel.Bereich.Enthaelt(new Kartenort(ort.X, ort.Y)) || LaufStelle(ziel, ort) is not { } st || st.Abstand > st.Halb + 8f) continue;
            var hinein = st.Halb - st.Abstand;
            if (tiefste is not { } t || hinein > t.Item1) tiefste = (hinein, st.Spiegel);
        }
        return tiefste;
    }

    /// <summary>Ein Stück Band aus Reihen zu je <paramref name="felder"/> + 1 Ecken: zwei Dreiecke je Feld, alle mit der Vorderseite nach oben.</summary>
    private static ArrayMesh BandNetz(List<Vector3> ecken, List<Vector2> uv, List<Vector2> uv2, List<Color> farben, int felder)
    {
        var breite = felder + 1;
        var indizes = new List<int>();
        for (var r = 0; r + 1 < ecken.Count / breite; r++)
            for (var j = 0; j < felder; j++)
            {
                int a = r * breite + j, b = a + 1, c = a + breite, d = c + 1;
                DreieckNachOben(indizes, ecken, a, b, c);
                DreieckNachOben(indizes, ecken, b, d, c);
            }
        return NetzAusFeldern(ecken, uv, uv2, farben, indizes);
    }

    /// <summary>
    /// Der Shader zeichnet nur Vorderseiten (<c>cull_back</c>), und Godot nimmt als Vorderseite die im Uhrzeigersinn: Die Ecken so ordnen, dass sie von oben
    /// gesehen im Uhrzeigersinn laufen (das Kreuzprodukt (b − a) × (c − a) zeigt dann nach unten).
    /// </summary>
    private static void DreieckNachOben(List<int> indizes, List<Vector3> ecken, int a, int b, int c)
    {
        var n = (ecken[b] - ecken[a]).Cross(ecken[c] - ecken[a]);
        if (n.Y <= 0) indizes.AddRange(new[] { a, b, c });
        else indizes.AddRange(new[] { a, c, b });
    }

    /// <summary>
    /// Wie hoch das Band an dieser Stelle mindestens liegen muss, damit der gezeichnete Boden nicht hindurchsticht: <see cref="WasserUeberBoden"/> über dem höchsten
    /// Punkt des Bodens (in einer Furt: des Weges, <see cref="FurtBoden"/>) quer bis drei Viertel der halben Breite – so weit ist das Wasser deckend, weiter außen läuft es
    /// aus – und längs bis zur nächsten Reihe. Der Boden ist nur alle 2 m bekannt: Das Bett eines schmalen oder schräg zum Raster laufenden Baches ist
    /// im Netz eine Kette einzelner Gruben, und ein Spiegel darin zerfiele in Flecken. Wo das Raster das Bett zeigt (ein breiter Fluss), liegt der Boden tiefer als der
    /// Spiegel, und es bleibt beim Spiegel.
    /// </summary>
    private float Wasserdeckel(Vector2 mitte, Vector2 richtung, float halb)
    {
        if (!BodenBewegt) return float.MinValue;
        var quer = new Vector2(-richtung.Y, richtung.X);
        var hoechst = float.MinValue;
        foreach (var laengs in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            foreach (var seite in new[] { -0.75f, -0.4f, 0f, 0.4f, 0.75f })
            {
                var p = mitte + richtung * (laengs * FlussSchritt / 2f) + quer * (seite * halb);
                var boden = FurtBoden(p.X, p.Y) is { } weg ? weg + WegHoehe : BodenHoehe(p.X, p.Y);
                hoechst = Mathf.Max(hoechst, boden);
            }
        return hoechst + WasserUeberBoden;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Offene Stellen, Mündungen
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>Was eine offene Stelle mit dem Eis macht: Furt (nur Randeis), Rinne (Fähre, quer durch das Randeis offen), Loch (Mündung, schnelle Stelle:
    /// rund offen), Kai (an einer Kaimauer bleibt vom Uferschaum ein schmaler Saum, und das Wasser friert dort früh wie im Hafenbecken).</summary>
    private enum Offenart { Furt, Rinne, Loch, Kai }

    /// <summary>Eine Stelle als Strecke <see cref="A"/>–<see cref="B"/> (beim Loch ein Punkt) mit halber Breite bzw. Halbmesser <see cref="Halb"/>.</summary>
    private readonly record struct OffeneStelle(Vector2 A, Vector2 B, float Halb, Offenart Art, string Name);

    /// <summary>
    /// Wo das Eis eines Gewässers zurückbleibt (Nutzer 30.10.2026): Furten über dieses Gewässer (Strecke und halbe Wegbreite, nur Randeis), Fährlinien
    /// (Rinne) und die Seiten der Hafenbecken als Kaimauer (schmaler Schaum, und das Wasser davor ist still: es friert wie das Becken). Die Löcher an
    /// Mündungen und schnellen Stellen gehen nicht über die Netzecken (im 2-m-Raster wurden sie eckig), sondern als Uniforms (<see cref="LoecherSetzen"/>).
    /// </summary>
    private List<OffeneStelle> OffeneStellen(Wasserform form, int nummer, string name)
    {
        var stellen = new List<OffeneStelle>();
        static Vector2 V(Kartenort o) => new((float)o.X, (float)o.Z);
        if (Uebergangsplan is { } plan)
        {
            foreach (var q in plan.Querungen)
                if (q.Art == Uebergangsart.Furt && q.Gewaesser == nummer) stellen.Add(new(V(q.Anfang), V(q.Ende), (float)q.Breite / 2, Offenart.Furt, "Furt"));
            foreach (var f in plan.Faehren)
                if (f.Gewaesser == name) stellen.Add(new(V(f.Von), V(f.Bis), FaehreHalb, Offenart.Rinne, f.Name));
        }
        foreach (var b in form.Becken)
            for (var k = 0; k < b.Umriss.Count; k++) // die Seiten des Beckens
                stellen.Add(new(V(b.Umriss[k]), V(b.Umriss[(k + 1) % b.Umriss.Count]), 0f, Offenart.Kai, b.Name));
        return stellen;
    }

    /// <summary>
    /// Wie weit das Eis am Ort zurückbleibt (1 = friert nie, weich ins Eis übergehend; an einer Furt <paramref name="furtOffen"/>, also nur Randeis) und
    /// wie nah eine Kaimauer ist (1 = dort).
    /// </summary>
    private static (float Offen, float Kai) Offenheit(List<OffeneStelle> stellen, Vector2 p, float furtOffen)
    {
        float offen = 0, kai = 0;
        foreach (var s in stellen)
        {
            var d = Mathf.Max(0, AbstandStrecke(p, s.A, s.B) - s.Halb);
            switch (s.Art)
            {
                case Offenart.Furt: offen = Mathf.Max(offen, furtOffen * (1f - Mathf.SmoothStep(OffenUmkreis, 2 * OffenUmkreis, d))); break;
                case Offenart.Rinne: offen = Mathf.Max(offen, 1f - Mathf.SmoothStep(RinneZugabe, 2 * RinneZugabe + 1f, d)); break;
                case Offenart.Loch: offen = Mathf.Max(offen, 1f - Mathf.SmoothStep(0f, LochRand, d)); break;
                case Offenart.Kai: kai = Mathf.Max(kai, 1f - Mathf.SmoothStep(2f, 8f, d)); break;
            }
        }
        return (offen, kai);
    }

    /// <summary>
    /// Eine Mündung: <see cref="Bach"/> tritt bei <see cref="Ort"/> (dem letzten Punkt, an dem er noch gilt) mit der halben Breite <see cref="Halb"/> in ein
    /// anderes Gewässer – einen anderen Lauf (<see cref="Ziel"/>) oder einen See (<see cref="ZielSee"/>).
    /// </summary>
    private sealed record Muendung(Vector2 Ort, float Halb, Wasserlauf Bach, Wasserlauf? Ziel, Wassersee? ZielSee);

    /// <summary>
    /// Die Mündungen: wo die Mittellinie eines Laufs in ein anderes Gewässer tritt, das dort gilt (oder aus ihm kommt). Dort mischt sich das Wasser:
    /// Im Bach bleibt ein Loch im Eis (halber Bach als Halbmesser, mindestens 1 m), im Fluss eine Rinne stromab am Ufer (<see cref="LoecherSetzen"/>),
    /// und das Bachwasser zieht als trüber Streifen in den Fluss (<see cref="MuendungenSetzen"/>).
    /// </summary>
    private static List<Muendung> Muendungen(Wasserform form)
    {
        var muendungen = new List<Muendung>();
        foreach (var lauf in form.Laeufe)
        {
            var andere = AndereGewaesser(form, lauf, null);
            if (andere.Count == 0 || lauf.Punkte.Count < 2) continue;
            bool? vorher = null;
            for (var i = 0; i < lauf.Punkte.Count; i++)
            {
                var o = lauf.Punkte[i].Ort;
                var drin = ImAnderenWasser(form, andere, new Vector2((float)o.X, (float)o.Z), lauf, null);
                if (vorher is { } v && v != drin)
                {
                    var aussen = lauf.Punkte[drin ? i - 1 : i]; // der letzte Punkt, an dem noch dieser Lauf gilt
                    var innen = lauf.Punkte[drin ? i : i - 1];
                    var ort = new Vector2((float)aussen.Ort.X, (float)aussen.Ort.Z);
                    var dort = form.An(innen.Ort.X, innen.Ort.Z);
                    muendungen.Add(new Muendung(ort, (float)aussen.Breite / 2, lauf, dort?.Lauf, dort?.See));
                }
                vorher = drin;
            }
        }
        return muendungen;
    }

    /// <summary>
    /// Die Löcher im Eis eines Gewässers als Uniforms (<c>loch_ort</c>, <c>loch_form</c>: rund und weich mit unregelmäßigem Rand statt über die Netzecken,
    /// die im 2-m-Raster eckige Löcher zeichneten): im Bach seine schnellen Stellen und das Loch an seiner Mündung; im Fluss oder See, in den ein Bach
    /// mündet, eine Rinne vom Mündungsort stromab am Ufer entlang (das Bachwasser hält sie offen), bzw. im See ein Loch.
    /// </summary>
    private void LoecherSetzen(ShaderMaterial stoff, Wasserlauf? lauf, Wassersee? see)
    {
        var orte = new List<Color>();
        var formen = new List<Color>();
        void Loch(Vector2 ort, float halb, Vector2 richtung, float laenge)
        {
            if (orte.Count >= LoecherJeStoff) return;
            orte.Add(new Color(ort.X, ort.Y, halb, LochRand));
            formen.Add(new Color(richtung.X, richtung.Y, laenge, 0));
        }
        foreach (var m in _muendungen)
        {
            if (lauf is not null && m.Bach == lauf) Loch(m.Ort, Mathf.Max(1f, m.Halb), Vector2.Right, 0);
            else if (lauf is not null && m.Ziel == lauf && LaufStelle(lauf, m.Ort) is { } st)
                Loch(m.Ort, m.Halb + 0.6f, st.Richtung, 6f + 3f * m.Halb);
            else if (see is not null && m.ZielSee == see) Loch(m.Ort, m.Halb + 1f, Vector2.Right, 0);
        }
        if (lauf is not null)
            foreach (var l in _wasserLoecher)
                if (l.Name.StartsWith("Schnelle Stelle " + lauf.Name, StringComparison.Ordinal)) Loch(l.A, l.Halb, Vector2.Right, 0);
        while (orte.Count < LoecherJeStoff) { orte.Add(new Color(0, 0, 0, 1)); formen.Add(new Color(1, 0, 0, 0)); }
        stoff.SetShaderParameter("loch_zahl", orte.Count(o => o.B > 0));
        GD.Print($"[wasser] {lauf?.Name ?? see?.Name}: {orte.Count(o => o.B > 0)} Löcher im Eis ({string.Join(", ", orte.Where(o => o.B > 0).Select(o => $"{o.R:0},{o.G:0} r {o.B:0.0}"))})");
        stoff.SetShaderParameter("loch_ort", Vierer(orte));
        stoff.SetShaderParameter("loch_form", Vierer(formen));
    }

    /// <summary>
    /// Die Mündungen in einen Fluss als Uniforms (<c>muendung</c>: quer, Stelle, Seite, Breite des Baches in den Laufachsen des Flusses): Der Shader
    /// zeichnet dort das Bachwasser als feine, trübere Linien, die am Ufer stromab ziehen (höchstens ein Drittel der örtlichen Flussbreite) und nach 25 bis
    /// 70 m weich auslaufen, mit dünnen Schaumstreifen an der Mischkante; den Uferschaum blendet er an der Mündung aus.
    /// </summary>
    private void MuendungenSetzen(ShaderMaterial stoff, Wasserlauf lauf)
    {
        var werte = new List<Color>();
        foreach (var m in _muendungen)
            if (m.Ziel == lauf && werte.Count < MuendungenJeStoff && LaufStelle(lauf, m.Ort) is { } st)
                werte.Add(new Color(st.Quer, st.Stelle, st.Quer >= 0 ? 1f : -1f, 2f * Mathf.Max(m.Halb, 0.8f)));
        var zahl = werte.Count;
        while (werte.Count < MuendungenJeStoff) werte.Add(new Color(0, 0, 0, 0));
        stoff.SetShaderParameter("muendung_zahl", zahl);
        if (zahl > 0) GD.Print($"[wasser] {lauf.Name}: {zahl} Mündungen ({string.Join(", ", werte.Take(zahl).Select(w => $"quer {w.R:0.0}, Stelle {w.G:0}, Bach {w.A:0.0} m"))})");
        stoff.SetShaderParameter("muendung", Vierer(werte));
    }

    /// <summary>Ein Feld von vec4 für den Shader als Zahlenreihe (je vier Werte; ein Farbfeld nimmt Godot für vec4-Felder nicht an).</summary>
    private static float[] Vierer(List<Color> werte) => werte.SelectMany(c => new[] { c.R, c.G, c.B, c.A }).ToArray();

    /// <summary>
    /// Wo ein Ort in den Laufachsen eines Laufs liegt (wie das Netz sie trägt, <see cref="FlussNetze"/>): quer (m, zur Seite von quer = (−z, x) der
    /// Fließrichtung), Stelle (m), Fließrichtung dort, Abstand zur Mittellinie, halbe Breite und Spiegel dort; null bei weniger als zwei Punkten.
    /// </summary>
    private static (float Quer, float Stelle, Vector2 Richtung, float Abstand, float Halb, float Spiegel)? LaufStelle(Wasserlauf lauf, Vector2 ort)
    {
        var p = lauf.Punkte;
        if (p.Count < 2) return null;
        var beste = 0;
        var besteD = double.MaxValue;
        for (var i = 0; i < p.Count; i++)
        {
            var (dx, dz) = (p[i].Ort.X - ort.X, p[i].Ort.Z - ort.Y);
            var d = dx * dx + dz * dz;
            if (d < besteD) (besteD, beste) = (d, i);
        }
        var (vor, nach) = (p[Math.Max(0, beste - 1)].Ort, p[Math.Min(p.Count - 1, beste + 1)].Ort);
        var richtung = new Vector2((float)(nach.X - vor.X), (float)(nach.Z - vor.Z)).Normalized();
        var quer = new Vector2(-richtung.Y, richtung.X);
        var rel = ort - new Vector2((float)p[beste].Ort.X, (float)p[beste].Ort.Z);
        return (rel.Dot(quer), (float)p[beste].Stelle + rel.Dot(richtung), richtung, (float)Math.Sqrt(besteD), (float)p[beste].Breite / 2, (float)p[beste].Spiegel);
    }

    /// <summary>
    /// Die schnellen Stellen eines Baches: wo der Spiegel steil fällt (Gefälle über ±<see cref="GefaelleFenster"/> m ab <see cref="SchnellAbGefaelle"/>), läuft
    /// das Wasser schnell und friert nicht. Je zusammenhängendes Stück die steilste Stelle, höchstens eine je <see cref="SchnellAbstand"/> m, ein Loch mit dem
    /// halben Bach als Halbmesser (mindestens 0,8 m).
    /// </summary>
    private static List<OffeneStelle> SchnelleStellen(Wasserlauf lauf)
    {
        var p = lauf.Punkte;
        var loecher = new List<OffeneStelle>();
        var (besteI, besteG) = (-1, 0.0);
        var letzteStelle = double.MinValue;
        void Abschliessen()
        {
            if (besteI < 0) return;
            if (p[besteI].Stelle - letzteStelle >= SchnellAbstand)
            {
                var ort = new Vector2((float)p[besteI].Ort.X, (float)p[besteI].Ort.Z);
                loecher.Add(new(ort, ort, Mathf.Max(0.8f, (float)p[besteI].Breite / 2), Offenart.Loch, $"Schnelle Stelle {lauf.Name} ({besteG * 100:0.0} %)"));
                letzteStelle = p[besteI].Stelle;
            }
            (besteI, besteG) = (-1, 0.0);
        }
        for (var i = GefaelleFenster; i + GefaelleFenster < p.Count; i++)
        {
            var (a, b) = (p[i - GefaelleFenster], p[i + GefaelleFenster]);
            var gefaelle = (a.Spiegel - b.Spiegel) / Math.Max(0.5, b.Stelle - a.Stelle);
            if (gefaelle >= SchnellAbGefaelle)
            {
                if (gefaelle > besteG) (besteI, besteG) = (i, gefaelle);
            }
            else Abschliessen();
        }
        Abschliessen();
        var steilste = 0.0;
        for (var i = GefaelleFenster; i + GefaelleFenster < p.Count; i++)
            steilste = Math.Max(steilste, (p[i - GefaelleFenster].Spiegel - p[i + GefaelleFenster].Spiegel) / Math.Max(0.5, p[i + GefaelleFenster].Stelle - p[i - GefaelleFenster].Stelle));
        GD.Print($"[wasser] {lauf.Name}: steilstes Gefälle {steilste * 100:0.0} %, {loecher.Count} schnelle Stellen (ab {SchnellAbGefaelle * 100:0.0} %)");
        return loecher;
    }

    private static float AbstandStrecke(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        var l2 = ab.LengthSquared();
        var t = l2 < 1e-6f ? 0 : Mathf.Clamp((p - a).Dot(ab) / l2, 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    /// <summary>Die Rechtecke der anderen Gewässer (ohne <paramref name="lauf"/> bzw. <paramref name="see"/>): nur dort lohnt die Frage nach einer Mündung.</summary>
    private static List<Kartenrechteck> AndereGewaesser(Wasserform form, Wasserlauf? lauf, Wassersee? see) =>
        form.Laeufe.Where(l => l != lauf).Select(l => l.Bereich).Concat(form.Seen.Where(s => s != see).Select(s => s.Bereich)).ToList();

    /// <summary>
    /// Liegt der Ort in einem anderen Gewässer, das dort gilt (Mündung: der Kern nimmt das mit dem tieferen Spiegel)? Dort blendet dieses Wasser aus und
    /// friert nicht (ein Bach friert nicht bis in den offenen Rhein hinein).
    /// </summary>
    private static bool ImAnderenWasser(Wasserform form, List<Kartenrechteck> andere, Vector2 ort, Wasserlauf? lauf, Wassersee? see)
    {
        var p = new Kartenort(ort.X, ort.Y);
        if (!andere.Any(r => r.Enthaelt(p)) || form.An(ort.X, ort.Y) is not { } w) return false;
        object? dort = (object?)w.Lauf ?? w.See;
        object? hier = (object?)lauf ?? see;
        return dort is not null && !ReferenceEquals(dort, hier);
    }

    /// <summary>
    /// Wie weit an diesem Ort ein anderes Gewässer übernimmt (0 = dieses, 1 = ein anderes): Deckkraft = 1 − Anteil, und um so viel bleibt das Eis zurück
    /// (es wächst im anderen). Sonst hart aus <see cref="ImAnderenWasser"/> (Mündung, Hafenbecken); in einem Wasserverbund (Befund 1, 09.11.2026)
    /// stetig aus dem Anteil des Sees im Kern (<see cref="Wasserverbund.AnteilAm"/>: im Bett des Laufs 0, in Lücke und See 1), längs mit dem Gewicht des
    /// Verbunds in die harte Regel überblendet – keine Zacken im 2-m-Raster, keine Naht an der Grenze, kein Eis in ehemaligem Land.
    /// </summary>
    /// <summary>
    /// Deckkraft des Verbundnetzes (NAS 09.11.2026: mit Deckkraft = Anteil des Sees lagen Band (1 − a) und Netz (a) halbdurchsichtig übereinander – wo a
    /// um 0,5 lag, schien der Grund durch: eine Landinsel an der Ecke, wo der Fluss im See endet, und ein Streifen am alten Ufer). Das Netz liegt unter dem
    /// Band und ist überall voll deckend, wo der See auch nur ein wenig Anteil hat (ab 5 %); das Band blendet darüber aus. Nur im Bett des Laufs, wo das
    /// Band ganz deckt, fehlt es; längs mit dem Gewicht in die harte Regel.
    /// </summary>
    private static float VerbundDeckkraft(Wasserform form, List<Kartenrechteck> andere, Vector2 ort, Wassersee see)
    {
        var hart = andere.Count > 0 && ImAnderenWasser(form, andere, ort, null, see) ? 0f : 1f;
        foreach (var v in form.Verbuende)
            if (ReferenceEquals(v.See, see) && v.AnteilAm(ort.X, ort.Y) is { } a)
                return Mathf.Lerp(hart, Mathf.SmoothStep(0f, 0.05f, (float)a.Anteil), (float)a.Gewicht);
        return hart;
    }

    /// <summary>
    /// Uferabstand einer Ecke des Bandes im Verbund: Dort, wo der See anschließt, ist das alte Ufer des Laufs kein Ufer mehr – der gemeinsame Abstand
    /// (Kern), längs mit dem Gewicht überblendet; sonst der eigene (NAS 09.11.2026: im Winter vereiste das Band am alten Ufer mitten im See).
    /// </summary>
    private static float BandUfer(Wasserform form, Wasserlauf lauf, Vector2 ort, float eigen, float tiefe)
    {
        // nur im eigenen Bett: Außerhalb seines Ufers (der Rand des Bandes, 15 % breiter) bleibt das Band unsichtbar – dort zeichnet das Verbundnetz.
        // Sonst endete das Band dort sichtbar mit einer harten Kante (Prüfhilfe „Ecken am äußeren Rand des Bandes sichtbar“; NAS 09.11.2026)
        if (eigen <= 0) return eigen;
        foreach (var v in form.Verbuende)
            if (ReferenceEquals(v.Lauf, lauf) && v.AbstandAm(ort.X, ort.Y) is { } s && v.AnteilAm(ort.X, ort.Y) is { } a)
                return Mathf.Lerp(eigen, Mathf.Max(eigen, VerbundUfer((float)s, tiefe)), (float)a.Gewicht);
        return eigen;
    }

    /// <summary>
    /// Der Uferabstand, den das Netz im Verbund trägt: der gemeinsame Abstand des Kerns (−s), im Wasser aber mindestens so weit, wie es die Tiefe mit der
    /// Böschung eines Sees ergäbe. Ohne Bildschirmtextur (Mittel, Niedrig) schätzt der Shader die Tiefe aus dem Uferabstand; am alten Ufer ist der gemeinsame
    /// Abstand klein, obwohl das Wasser dort tief ist (NAS 09.11.2026: dort war das Wasser hell und halb durchsichtig). Auch das Eis wächst so nur vom echten Ufer.
    /// </summary>
    private static float VerbundUfer(float s, float tiefe) => s < 0 ? Mathf.Max(-s, tiefe / (float)Wasserform.Seeboeschung) : -s;

    private static float Fremdanteil(Wasserform form, List<Kartenrechteck> andere, Vector2 ort, Wasserlauf? lauf, Wassersee? see)
    {
        var hart = andere.Count > 0 && ImAnderenWasser(form, andere, ort, lauf, see) ? 1f : 0f;
        foreach (var v in form.Verbuende)
        {
            if (!(lauf is not null && ReferenceEquals(v.Lauf, lauf)) && !(see is not null && ReferenceEquals(v.See, see))) continue;
            if (v.AnteilAm(ort.X, ort.Y) is not { } a) continue;
            var verbund = lauf is not null ? (float)a.Anteil : 1f - (float)a.Anteil;
            return Mathf.Lerp(hart, verbund, (float)a.Gewicht);
        }
        return hart;
    }

    private static ArrayMesh NetzAusFeldern(List<Vector3> ecken, List<Vector2> uv, List<Vector2> uv2, List<Color> farben, List<int>? indizes)
    {
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = ecken.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = ecken.Select(_ => Vector3.Up).ToArray();
        arrays[(int)Mesh.ArrayType.TexUV] = uv.ToArray();
        arrays[(int)Mesh.ArrayType.TexUV2] = uv2.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = farben.ToArray();
        if (indizes is not null) arrays[(int)Mesh.ArrayType.Index] = indizes.ToArray();
        var netz = new ArrayMesh();
        netz.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        netz.CustomAabb = netz.GetAabb().Grow(1f); // die Wellen heben und schieben die Fläche ein Stück über das Netz hinaus
        return netz;
    }

    // ═════════════════════════════════════════════════════════════════════════
    //  Seen, Teiche und Hafenbecken
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Die Fläche eines Sees: Dreiecke genau über den Zellen des Bodens (so klafft am Ufer kein Spalt), alle auf der Höhe des Wasserstands, in Ultra
    /// geviertelt. COLOR = (Abstand zum Umriss / 32, Eiskanal, 1, Deckkraft).
    /// </summary>
    private ArrayMesh? SeeNetz(Wassersee see, Wasserform form, float abstand, Wasserart art)
    {
        if (see.Umriss.Count < 3 || _gelaende is null) return null;
        if (form.Verbuende.Any(v => ReferenceEquals(v.See, see))) return VerbundNetz(see, form, abstand, art);
        var umriss = see.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray();
        var ecken = FeinerTeilen(RasterDreiecke(umriss, 0, (float)see.Wasserstand), abstand);
        if (ecken.Count == 0) return null;
        var andere = AndereGewaesser(form, null, see);
        var offenStellen = OffeneStellen(form, see.Nummer, see.Name);
        var furtOffen = Wasserarten.FurtOffen(art, 8f);
        var werte = new Dictionary<(float, float), Color>();
        var farben = ecken.Select(e =>
        {
            if (werte.TryGetValue((e.X, e.Z), out var c)) return c;
            var ort = new Vector2(e.X, e.Z);
            var rand = (float)see.Randabstand(new Kartenort(e.X, e.Z));
            var fremd = andere.Count > 0 && ImAnderenWasser(form, andere, ort, null, see);
            var (offen, kai) = Offenheit(offenStellen, ort, furtOffen);
            return werte[(e.X, e.Z)] = new Color(Mathf.Clamp(rand / 32f, 0f, 1f), Wasserarten.EisKanal(fremd ? 1f : offen, kai), 1f, fremd ? 0f : 1f);
        }).ToList();
        return DreieckNetz(ecken, farben);
    }

    /// <summary>So viel (m) liegt das Verbundnetz unter dem Spiegel: Wo es mit dem Band des Laufs übereinanderliegt, zeichnet das Band darüber.</summary>
    private const float VerbundTiefer = 0.005f;

    /// <summary>
    /// Das Netz eines Sees im Wasserverbund (Befund 1, 09.11.2026; Kern: <see cref="Wasserform.Verbuende"/>): Dreiecke über allen Zellen des Bodens im Bereich
    /// des Sees und seiner Verbünde, an denen eine Ecke im Wasser liegt (gemeinsamer Abstand unter 0,5 m) – See, geschlossene Lücke und der Rand des Laufs
    /// in einem Netz. Je Ecke COLOR.r = (4 − Abstand) / 36 (der gemeinsame Abstand des Kerns ist im Wasser negativ, der Uferabstand im Shader positiv) (Shader-Uniform <c>ufer_vorzeichen</c>): Die Kante liegt je Bildpunkt dort, wo
    /// der Abstand 0 wird, ohne Zacken im Raster; Höhe = Spiegel aus <see cref="Wasserform.An"/> (quer stetig) knapp darunter; Deckkraft = Anteil des Sees
    /// (im Bett des Laufs zeichnet dessen Band, das nicht ausblendet und seine Strömung, Wellen und Mündungslinien behält). Außerhalb der Verbünde gilt der
    /// Randabstand des Sees wie bisher.
    /// </summary>
    private ArrayMesh? VerbundNetz(Wassersee see, Wasserform form, float abstand, Wasserart art)
    {
        var verbuende = form.Verbuende.Where(v => ReferenceEquals(v.See, see)).ToList();
        var rahmen = verbuende.Aggregate(see.Bereich, (r, v) => new Kartenrechteck(Math.Min(r.X0, v.Bereich.X0), Math.Min(r.Z0, v.Bereich.Z0),
            Math.Max(r.X1, v.Bereich.X1), Math.Max(r.Z1, v.Bereich.Z1)));
        float Abstand(float x, float z)
        {
            double? s = null;
            foreach (var v in verbuende)
                if (v.AbstandAm(x, z) is { } d) s = Math.Min(s ?? double.MaxValue, d);
            if (s is { } wert) return (float)wert;
            var rand = see.Randabstand(new Kartenort(x, z));
            return (float)(see.Enthaelt(new Kartenort(x, z)) ? -rand : rand);
        }
        const float z = (float)Hoehenraster.Abstand;
        var (i0, j0) = ((int)Math.Floor(rahmen.X0 / z), (int)Math.Floor(rahmen.Z0 / z));
        var (i1, j1) = ((int)Math.Floor(rahmen.X1 / z), (int)Math.Floor(rahmen.Z1 / z));
        var eckS = new Dictionary<(int, int), float>();
        float S(int i, int j) => eckS.TryGetValue((i, j), out var w) ? w : eckS[(i, j)] = Abstand(i * z, j * z);
        var dreiecke = new List<Vector3>();
        for (var j = j0; j <= j1; j++)
            for (var i = i0; i <= i1; i++)
            {
                if (Mathf.Min(Mathf.Min(S(i, j), S(i + 1, j)), Mathf.Min(S(i + 1, j + 1), S(i, j + 1))) >= 0.5f) continue;
                var (v00, v10, v11, v01) = (new Vector3(i * z, 0, j * z), new Vector3((i + 1) * z, 0, j * z), new Vector3((i + 1) * z, 0, (j + 1) * z), new Vector3(i * z, 0, (j + 1) * z));
                dreiecke.AddRange(new[] { v00, v10, v11, v00, v11, v01 });
            }
        var ecken = FeinerTeilen(dreiecke, abstand);
        if (ecken.Count == 0) return null;
        var andere = AndereGewaesser(form, null, see);
        var offenStellen = OffeneStellen(form, see.Nummer, see.Name);
        var furtOffen = Wasserarten.FurtOffen(art, 8f);
        var werte = new Dictionary<(float, float), (float Hoehe, Color Farbe)>();
        var farben = new List<Color>(ecken.Count);
        for (var n = 0; n < ecken.Count; n++)
        {
            var e = ecken[n];
            if (!werte.TryGetValue((e.X, e.Z), out var wert))
            {
                var ort = new Vector2(e.X, e.Z);
                var s = Abstand(e.X, e.Z);
                var stelle = form.An(e.X, e.Z);
                var eigen = stelle is { } st && (ReferenceEquals(st.See, see) || verbuende.Any(v => ReferenceEquals(v.Lauf, st.Lauf)));
                var hoehe = (eigen ? (float)stelle!.Value.Spiegel : (float)see.Wasserstand) - VerbundTiefer;
                var fremd = Fremdanteil(form, andere, ort, null, see);
                var deckkraft = VerbundDeckkraft(form, andere, ort, see);
                var (offen, kai) = Offenheit(offenStellen, ort, furtOffen);
                // Uferabstand im Netz positiv im Wasser: (4 − s) / 36 (NAS 09.11.2026: mit (s + 4) / 36 stand er verkehrt herum, die Seen waren unsichtbar)
                var ufer = VerbundUfer(s, hoehe + VerbundTiefer - BodenHoehe(e.X, e.Z));
                werte[(e.X, e.Z)] = wert = (hoehe, new Color(Mathf.Clamp((ufer + 4f) / 36f, 0f, 1f), Wasserarten.EisKanal(Mathf.Lerp(offen, 1f, fremd), kai), 1f, deckkraft));
            }
            ecken[n] = new Vector3(e.X, wert.Hoehe, e.Z);
            farben.Add(wert.Farbe);
        }
        return DreieckNetz(ecken, farben);
    }

    /// <summary>
    /// Das Wasser eines Hafenbeckens: wie ein See genau über den Zellen des Bodens, auf dem Spiegel des Gewässers vor der Kaimauer. Wo der Fluss beginnt, ist
    /// die Deckkraft der Ecke 0 – dort blendet das Becken in das Band des Flusses über (das zuerst gezeichnet wird). COLOR = (Uferabstand / 32, Eiskanal 0,5, 1, Deckkraft);
    /// der Uferabstand ist der Abstand zu Kaimauer und Seiten (das Eis wächst von dort), höchstens die doppelte Tiefe (ohne Tiefentextur, Stufe Niedrig,
    /// liest der Shader den halben Uferabstand als Tiefe).
    /// </summary>
    private ArrayMesh? BeckenNetz(Hafenbecken becken, Wasserform form, float abstand)
    {
        if (_gelaende is null) return null;
        var spiegel = (float)becken.Spiegel;
        var umriss = becken.Umriss.Select(o => new Vector2((float)o.X, (float)o.Z)).ToArray();
        var ecken = FeinerTeilen(RasterDreiecke(umriss, 0, spiegel), abstand);
        if (ecken.Count == 0) return null;
        // Die Seiten am Land: Kaimauer (0–1) und die beiden Seiten (1–2, 3–0); die vierte (2–3) liegt zum Fluss hin
        var land = umriss.Length == 4 ? new[] { (umriss[0], umriss[1]), (umriss[1], umriss[2]), (umriss[3], umriss[0]) }
            : umriss.Select((a, k) => (a, umriss[(k + 1) % umriss.Length])).ToArray();
        var farben = ecken.Select(e =>
        {
            var p = new Vector2(e.X, e.Z);
            var mauer = land.Min(s => AbstandStrecke(p, s.Item1, s.Item2));
            var ufer = Mathf.Min((spiegel - BodenHoehe(e.X, e.Z)) * 2f, mauer);
            return new Color(Mathf.Clamp(ufer / 32f, 0f, 1f), Wasserarten.EisKanal(0f), 1f, form.IstWasser(e.X, e.Z) ? 0f : 1f);
        }).ToList();
        return DreieckNetz(ecken, farben);
    }

    /// <summary>Ultra (Netz 1 m): jedes Dreieck des 2-m-Rasters in vier teilen; sonst unverändert.</summary>
    private static List<Vector3> FeinerTeilen(List<Vector3> dreiecke, float abstand)
    {
        if (abstand >= (float)Hoehenraster.Abstand * 0.75f) return dreiecke;
        var fein = new List<Vector3>(dreiecke.Count * 4);
        for (var i = 0; i + 2 < dreiecke.Count; i += 3)
        {
            var (a, b, c) = (dreiecke[i], dreiecke[i + 1], dreiecke[i + 2]);
            var (ab, bc, ca) = ((a + b) / 2, (b + c) / 2, (c + a) / 2);
            fein.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
        }
        return fein;
    }

    /// <summary>Eine Dreiecksliste (je drei Ecken) mit Vorderseite nach oben als Netz für den Wasser-Shader (ohne Laufachsen).</summary>
    private static ArrayMesh DreieckNetz(List<Vector3> ecken, List<Color> farben)
    {
        var indizes = new List<int>(ecken.Count);
        for (var i = 0; i + 2 < ecken.Count; i += 3) DreieckNachOben(indizes, ecken, i, i + 1, i + 2);
        var leer = ecken.Select(_ => Vector2.Zero).ToList();
        return NetzAusFeldern(ecken, leer, leer, farben, indizes);
    }

    /// <summary>Fläche eines Vielecks (m², Gaußsche Trapezformel).</summary>
    private static double Flaecheninhalt(IReadOnlyList<Kartenort> umriss)
    {
        var summe = 0.0;
        for (var i = 0; i < umriss.Count; i++)
        {
            var (a, b) = (umriss[i], umriss[(i + 1) % umriss.Count]);
            summe += a.X * b.Z - b.X * a.Z;
        }
        return Math.Abs(summe) / 2;
    }
}
