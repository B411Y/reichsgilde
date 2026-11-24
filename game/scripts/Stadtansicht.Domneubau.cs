using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Reichsgilde.Core;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde;

/// <summary>
/// Die Dome des Neubaus (Paket 1: Dom katholisch) – Teil der <c>partial class Stadtansicht</c>.
///
/// Außen steht <c>gebaeude/neubau/dom_&lt;art&gt;.glb</c> (Knoten „Haus“, „Dach“, „Scheiben“ wie beim Rathaus), innen liegt
/// <c>dom_&lt;art&gt;_innen.glb</c> als „InteriorRoot“ im Haus selbst: Die Raumkamera steht auf dem Anker <c>Blick_Auge</c> im Mittelgang,
/// durch die Fenster sieht man die echte Stadt. Die Hülle ist fest je Karte, die Einrichtung folgt dem Stadtglauben: die Knoten
/// <c>Einrichtung_Katholisch</c> (mit <c>Weihrauch</c>), <c>Einrichtung_Evangelisch</c> (mit <c>Kreuz_Evangelisch</c>) und
/// <c>Einrichtung_Simultan</c> werden wie bisher die Grundkörper umgeschaltet.
///
/// Messe und Prediger docken an die Anker des Modells an: <c>Glaeubig_&lt;n&gt;</c> (Sitzplätze, vorn zuerst; extras <c>links</c>),
/// <c>Predigt_1</c> (Kanzel), <c>Anker_Bischof</c>, <c>Anker_Pfarrer</c>, <c>Feuer_Kerze_&lt;n&gt;</c>, <c>Feuer_Altarkerze_&lt;n&gt;</c>,
/// <c>Licht_Raum_&lt;n&gt;</c>, <c>Tuer_Dom_Stadt</c> (Portal: führt hinaus wie die Haustür). Regeln und Texte der Messe sind die des alten
/// Doms (<see cref="DomZustandSetzen"/>, gemeinsam mit <see cref="DomBauen"/>).
///
/// Die Karte wählt den Neubau über die Katalog-Kennung <c>dom_katholisch</c> (Paket 3 setzt ihn ein); bis dahin setzt ihn die Startoption
/// <c>--neuerdom=1</c> an die Stelle des alten.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Der eine Raum des Doms in der Raumansicht.</summary>
    public const string RaumDomschiff = "dom:schiff";

    private static readonly bool DomNeuOption = NeubauOption("--neuerdom") == "1";
    private Node3D? _domNeuHuelle;   // Wurzel des Modells in der Stadt
    private Node3D? _domNeuInnen;    // „InteriorRoot“
    private ShaderMaterial? _domNeuGlas;   // Butzenglas mit Farbton (Fenster des Doms von innen)

    /// <summary>
    /// Das Modell des neuen Doms für dieses Gebäude der Karte, null: der bisherige Dom. Die Karte wählt die Hülle über die Kennung des
    /// Katalogs (<c>dom_katholisch</c>; fest je Karte); <c>--neuerdom=1</c> setzt den Neubau zur Prüfung auch an die Stelle des alten <c>dom</c>.
    /// </summary>
    private static string? DomNeuDatei(Kartengebaeude g)
    {
        var datei = g.Katalog == "dom_katholisch" || (DomNeuOption && g.Katalog == "dom") ? NeubauOrdner + "dom_katholisch" : null;
        return datei is not null && ResourceLoader.Exists($"res://assets/modelle/{datei}.glb") ? datei : null;
    }

    /// <summary>Lage eines Ankers im „InteriorRoot“, wie die Helfer der Innenräume sie erwarten (relativ zu <c>a.Mitte</c>).</summary>
    private static Vector3 DomNeuOrt(SonderbauAnsicht a, Node3D wurzel, Node3D anker) => wurzel.ToLocal(anker.GlobalPosition) - a.Mitte;

    /// <summary>Blickrichtung eines Ankers im „InteriorRoot“ (Drehung um die Hochachse, 0 = Norden wie bei allen Plätzen).</summary>
    private static float DomNeuBlick(Node3D wurzel, Node3D anker) => (wurzel.GlobalTransform.AffineInverse() * anker.GlobalTransform).Basis.GetEuler().Y;

    private void DomNeuBauen(Kartengebaeude g, string datei)
    {
        var lage = ModellLage(g);
        var p = lage.Origin;
        var drehung = DrehungVon(g);
        var basis = new Basis(Vector3.Up, drehung);
        // Maße aus tools/modelle/neubau/dom_masse.py: Langhaus 15,8 m breit, Querhaus 18,8 m, 35 m lang, Türme 5,6 × 5,6 m vorn (im Spiel +Z), 42 m mit Kreuz
        var a = SonderbauAnlegen("Dom", string.IsNullOrEmpty(g.Name) ? "Dom" : g.Name, p, new Vector3(18.8f, 20, 35.5f), basis * new Vector3(0, 45, 14.7f),
            zoom: 32, reichweite: 17, raumansicht: true, drehung: drehung);
        var huelle = ModellEinsetzen(datei, this, p, new ModellZustand(), "Dom");
        if (huelle is null || Modell(datei + "_innen") is not { } szene)
        {
            GD.PushWarning($"Dom: {datei} oder der Innenraum fehlt.");
            return;
        }
        huelle.Wurzel.Transform = lage;
        _kirchenFenster.Add(huelle);
        FenstergruppeSetzen(huelle, Fenstergruppe.Kirche);
        a.Huelle = huelle.Wurzel;
        _domNeuHuelle = huelle.Wurzel;
        // Stein in seiner eigenen Farbe: kein Putzton und keine Akzentfarbe aus dem Standort
        foreach (var teil in huelle.Wurzel.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            teil.SetInstanceShaderParameter("plaster_tint", new Color(1, 1, 1));
            teil.SetInstanceShaderParameter("accent_tint", new Color(1, 1, 1));
        }
        foreach (var x in new[] { -5.1f, 5.1f })
            a.ZusatzKlicks.Add(Klickflaeche("Dom", p + basis * new Vector3(x, 21, 14.7f), new Vector3(5.6f, 42, 5.6f), drehung: drehung)); // die Türme
        a.Raeume.Add(new Raum(RaumDomschiff, "Domschiff", Vector3.Zero, new Vector3(5, 4, 6)));

        // „InteriorRoot“ im Haus: Er ist zugleich der Knoten „Innen“ der Ansicht – Figuren, Flammen und die Tafel hängen daran
        var wurzel = new Node3D { Name = "InteriorRoot", Visible = false, ProcessMode = ProcessModeEnum.Disabled };
        huelle.Wurzel.AddChild(wurzel);
        var raum = szene.Instantiate<Node3D>();
        raum.Name = "Raum_dom";
        wurzel.AddChild(raum);
        NeubauEinkleiden(datei + "_innen", raum);
        // Farbiges Glas (Nutzer, 23.11.2026): dasselbe Butzenglas mit kräftigem blauviolettem Ton statt des grünlichen Waldglases; der Blick
        // hinaus bleibt (das Glas tönt das Bild dahinter nur)
        _domNeuGlas ??= new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/shader/butzenglas.gdshader"), ResourceName = "Domglas" };
        _domNeuGlas.SetShaderParameter("glasfarbe", new Color(0.50f, 0.52f, 0.92f));
        foreach (var glas in raum.FindChildren("Glas_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            for (var i = 0; i < (glas.Mesh?.GetSurfaceCount() ?? 0); i++) glas.SetSurfaceOverrideMaterial(i, _domNeuGlas);
        a.Innen?.QueueFree(); // die leere Bühne unter der Stadt wird nicht gebraucht
        a.Innen = wurzel;
        _domNeuInnen = wurzel;
        Node3D? Anker(string name) => raum.FindChild(name, true, false) as Node3D;
        Vector3 Ort(Node3D anker) => DomNeuOrt(a, wurzel, anker);

        var messe = new bool[1];
        // Licht: Grundlicht unter den Gewölben (ohne Schatten), zur Messe der Schein der Kerzen im Chor
        foreach (var licht in raum.FindChildren("Licht_Raum_*", nameof(Node3D), true, false).OfType<Node3D>())
            licht.AddChild(new OmniLight3D { LightColor = new Color(0.95f, 0.88f, 0.76f), LightEnergy = 2.6f, OmniRange = 18f, ShadowEnabled = false });
        // Portal von innen: führt hinaus wie die Haustür
        if (Anker("Tuer_Dom_Stadt") is { } tuer)
        {
            var flaeche = Klickflaeche(NeubauAusgang, wurzel.ToLocal(tuer.GlobalPosition), new Vector3(ExtraZahlKomma(tuer, "breite", 2.4f), ExtraZahlKomma(tuer, "hoehe", 3.6f), 0.5f), wurzel);
            a.Raumklicks.Add(flaeche);
        }
        foreach (var kerze in raum.FindChildren("Feuer_Kerze_*", nameof(Node3D), true, false).OfType<Node3D>())
            Kerzenflamme(a, Ort(kerze), () => messe[0], wurzel);

        // Einrichtung je Stadtglaube: die Netze des Modells und je ein Knoten für Figuren und Flammen
        var katholisch = new Node3D { Name = "Katholisch" };
        var evangelisch = new Node3D { Name = "Evangelisch" };
        wurzel.AddChild(katholisch);
        wurzel.AddChild(evangelisch);
        foreach (var kerze in raum.FindChildren("Feuer_Altarkerze_*", nameof(Node3D), true, false).OfType<Node3D>())
            Kerzenflamme(a, Ort(kerze), () => messe[0], katholisch);
        var teileKatholisch = new[] { Anker("Einrichtung_Katholisch"), Anker("Weihrauch"), katholisch };
        var teileEvangelisch = new[] { Anker("Einrichtung_Evangelisch"), evangelisch };
        var kreuz = Anker("Kreuz_Evangelisch");
        var schranke = Anker("Einrichtung_Simultan");
        var weihrauch = Anker("Weihrauch");

        var bischofAnker = Anker("Anker_Bischof");
        var pfarrerAnker = Anker("Anker_Pfarrer");
        var kanzel = Anker("Predigt_1");
        Arbeitsplatz Stehplatz(Node3D? anker) => anker is null ? new Arbeitsplatz(Vector3.Zero, Sued, Bewegung.Beten, Werkzeug.Keins)
            : new Arbeitsplatz(Ort(anker), DomNeuBlick(wurzel, anker), Bewegung.Beten, Werkzeug.Keins);
        var pfarrerPlatz = Stehplatz(pfarrerAnker);
        // Geistliche aus dem Baukasten (wie der Prediger der Kirchen der Häuser): älterer Mensch im hellen Gewand, der Pfarrer mit Barett
        var bischof = InnenFigurBauen(a, Stehplatz(bischofAnker), FigurenBaukasten.Statist("Dom:Bischof", Volk.Mensch, false, "Prediger", grau: true), "Stehen", "Stehen", eltern: katholisch);
        var pfarrer = InnenFigurBauen(a, pfarrerPlatz, FigurenBaukasten.Statist("Dom:Pfarrer", Volk.Mensch, false, "Prediger", Kopfbedeckung.Barett), "Stehen", "Stehen", eltern: evangelisch);
        a.Besucher.Add(bischof);
        a.Besucher.Add(pfarrer);
        if (bischofAnker is not null)
            FeuerAnlegen(a, Ort(bischofAnker) + new Vector3(0, 2.6f, 0), null, lichtAn: 1.8f, lichtAus: 0.25f, an: () => messe[0], reichweite: 11);

        // Sitzplätze der Gemeinde aus den Ankern, vorn zuerst
        var sitzplaetze = raum.FindChildren("Glaeubig_*", nameof(Node3D), true, false).OfType<Node3D>()
            .Select(k => (Nummer: int.TryParse(k.Name.ToString()["Glaeubig_".Length..], out var n) ? n : 0, Knoten: k))
            .OrderBy(k => k.Nummer)
            .Select(k => (Platz: new Arbeitsplatz(Ort(k.Knoten), DomNeuBlick(wurzel, k.Knoten), Bewegung.Beten, Werkzeug.Keins, true), Links: ExtraZahl(k.Knoten, "links", 0) == 1))
            .ToList();
        var reihenfolge = Enumerable.Range(0, sitzplaetze.Count).ToList();
        var glaeubige = sitzplaetze.Select((x, i) => GlaeubigerBauen(a, x.Platz, i)).ToList();
        var betende = new Dictionary<int, (Arbeiterfigur Figur, int Platz)>();
        // Tafel über dem Mittelgang: scharf auch im Pixel-Look (wie die Schilder des Rathauses, RathausNeuTexteScharf)
        var tafel = new Label3D
        {
            Position = new Vector3(0, 5.6f, 2), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, FixedSize = true,
            PixelSize = SchildPixel, FontSize = 48, OutlineSize = 12, LineSpacing = 2,
            Modulate = new Color(1f, 0.93f, 0.72f), OutlineModulate = new Color(0.10f, 0.07f, 0.05f),
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic, RenderPriority = 10,
        };
        wurzel.AddChild(tafel);
        _rathausNeuTexte.Add(tafel);
        a.Infotafel = tafel;
        GD.Print($"[domneu] {datei}: {sitzplaetze.Count} Sitzplätze, Kanzel {(kanzel is null ? "fehlt" : "da")}, Portal {(Anker("Tuer_Dom_Stadt") is null ? "fehlt" : "da")}");

        a.Zustand = (zeit, arbeitszeit, aktiv) =>
        {
            if (_simulation?.Wirtschaft is not { } w) return;
            var (zeigtKatholisch, zeigtEvangelisch, istSimultan, text) = DomZustandSetzen(a, w, zeit, messe, glaeubige, betende, sitzplaetze, reihenfolge);
            foreach (var teil in teileKatholisch) if (teil is not null) teil.Visible = zeigtKatholisch;
            foreach (var teil in teileEvangelisch) if (teil is not null) teil.Visible = zeigtEvangelisch;
            if (schranke is not null) schranke.Visible = istSimultan;
            if (kreuz is not null) kreuz.Visible = zeigtEvangelisch && !istSimultan; // in der Simultankirche steht das Retabel dahinter
            bischof.Koerper.Visible = arbeitszeit;
            pfarrer.Koerper.Visible = arbeitszeit;
            // Zur Messe predigt der Pfarrer von der Kanzel.
            var predigt = messe[0] && !istSimultan && kanzel is not null;
            pfarrer.Koerper.Position = a.Mitte + (predigt ? Ort(kanzel!) : pfarrerPlatz.Ort);
            pfarrer.Koerper.Rotation = new Vector3(0, FigurenBaukasten.Modelldrehung(predigt ? DomNeuBlick(wurzel, kanzel!) : pfarrerPlatz.Drehung), 0);
            // Die Ampel schwingt an ihrer Kette (Ursprung des Knotens am Aufhängepunkt)
            if (weihrauch is not null && weihrauch.Visible) weihrauch.Rotation = new Vector3(0, 0, Mathf.Sin(Time.GetTicksMsec() / 1000f * 1.6f) * 0.05f);
            a.Infotafel!.Text = text;
        };
    }

    /// <summary>Der gezeigte Raum ist das Schiff des neuen Doms: dessen „InteriorRoot“, sonst null.</summary>
    private Node3D? DomNeuRaumKnoten() =>
        _innen is not null && _raum is not null && _raum.Kennung == RaumDomschiff && _domNeuInnen is { } wurzel && IsInstanceValid(wurzel) && _innen.Innen == wurzel ? wurzel : null;

    /// <summary>Zeigt den Dom von innen: „InteriorRoot“ an, die Scheiben der Hülle aus, die Kamera auf dem Blick des Raums.</summary>
    private void DomNeuZeigen(Node3D wurzel)
    {
        if (_neubauInnen is not null && _neubauInnen != wurzel) NeubauInnenAus();
        _neubauInnen = wurzel;
        wurzel.Visible = true;
        wurzel.ProcessMode = ProcessModeEnum.Inherit;
        if (_domNeuHuelle?.FindChild("Scheiben", true, false) is Node3D scheiben) scheiben.Visible = false;
        var auge = wurzel.FindChild("Blick_Auge", true, false) as Node3D;
        NeubauKameraSetzen(auge, wurzel.FindChild("Blick_Ziel", true, false) as Node3D);
        GD.Print($"[domneu] Domschiff im Haus, Kamera {(auge is null ? "ohne Anker" : _raumKamera.GlobalPosition.ToString("0.00"))}");
    }
}
