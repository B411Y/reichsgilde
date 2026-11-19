using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Reichsgilde;

/// <summary>
/// Räume des Wohnhauses in 3D (Neubau-Probe, Wohnhaus Mensch): Hinterzimmer und Bibliothek – Teil der <c>partial class Stadtansicht</c>.
///
/// <b>Modelle.</b> Je Raum <c>gebaeude/neubau/wohnhaus_mensch_s1_g1_&lt;raum&gt;_innen.glb</c> (gebaut von <c>tools/modelle/raeume/</c>),
/// im Grundriss des Hauses: dieselben Innenwände und Fensteröffnungen wie die Wohnstube, durch die Fenster sieht man die Stadt.
/// Im Spiel hängt jeder Raum als eigener Knoten „InteriorRoot_&lt;Raum&gt;“ im Haus neben „InteriorRoot“ (der Wohnstube); die
/// Raumkamera zeigt immer nur einen davon (der andere ist unsichtbar und ohne Verarbeitung, seine Klickflächen sind damit aus).
///
/// <b>Wann es den Raum gibt.</b> Wie im Kern: <c>Wohnungswesen.HatRaum</c> (Räume je Stufe, Datenversion 87). Dazu muss das Haus
/// des Spielers der Neubau sein (<c>--neubauprobe=1</c>, bisher nur Stufe 1). Die Startoption <c>--neubauraeume=Hinterzimmer,Bibliothek</c>
/// zeigt die Räume ohne Anbau, nur für Bilder (das Haus der Stufe 1 hat nach den Regeln noch keinen Raum).
///
/// <b>Wege.</b> In der Wohnstube steht je Raum eine Zimmertür unter der Galerie (Knoten „Zimmertuer_&lt;Raum&gt;“, sichtbar nur mit dem
/// Raum; Anker „Tuer_Wohnstube_&lt;Raum&gt;“), im Raum die Tür zurück (Anker „Tuer_&lt;Raum&gt;_Wohnstube“). Klick: die Hand greift die
/// Klinke, dann die Schwarzblende (wie die Türen im Rathaus-Neubau), dahinter <see cref="RaumWaehlen(string)"/>.
///
/// <b>Gegenstände.</b> „Gegenstand_&lt;Name&gt;“ wie in der Wohnstube (Leuchten, Namensschild, Klick in Hauptszene.RaumAngeklickt):
/// Kartentisch → Hinterzimmer (Banden, Erpresserbriefe, Wächter), Lesepult → Familienbuch mit den Lehrschriften. Alles andere ist Ausstattung.
/// </summary>
public partial class Stadtansicht
{
    /// <summary>Kennungen der Räume in der Raumleiste.</summary>
    public const string RaumHinterzimmer = "wohnhaus:hinterzimmer", RaumBibliothek = "wohnhaus:bibliothek";

    /// <summary>Die Räume: Kennung im Spiel, Kennung im Kern (<c>wohnen.raeume.arten</c>), Titel in der Raumleiste.</summary>
    private static readonly (string Kennung, string Kern, string Titel)[] Wohnraeume =
    {
        (RaumHinterzimmer, "Hinterzimmer", "Hinterzimmer"),
        (RaumBibliothek, "Bibliothek", "Bibliothek"),
    };

    /// <summary>Vorsatz der Klickflächen auf den Zimmertüren („neubau:raumtuer:Hinterzimmer“, zurück „…:Wohnstube“).</summary>
    private const string WohnraumTuer = "neubau:raumtuer:";

    /// <summary>Startoption <c>--neubauraeume=Hinterzimmer,Bibliothek</c> (mit <c>--neubauprobe=1</c>): die Räume zeigen, auch ohne Anbau – nur für Bilder.</summary>
    private static readonly HashSet<string> NeubauRaeumeOption = new((NeubauOption("--neubauraeume") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);

    /// <summary>Namensschilder der Gegenstände in den Räumen (was der Klick öffnet).</summary>
    private static readonly Dictionary<string, string> WohnraumGegenstandTitel = new()
    {
        ["Kartentisch"] = "Kartentisch · Hinterzimmer", ["Lesepult"] = "Lesepult · Lehrschriften",
    };

    /// <summary>Die Datei des Hauses, in dem die Räume liegen: die Grundform und Stufe des Wohnhauses in der Innenansicht (Meta „neubau_datei“).</summary>
    private string WohnraumHaus() =>
        _wohnhausAnsicht is { } a && _neubauHaeuser.TryGetValue(a.Kennung, out var haus) && IsInstanceValid(haus) && haus.HasMeta(NeubauDateiMeta)
            ? haus.GetMeta(NeubauDateiMeta).AsString() : NeubauWohnhaus;

    /// <summary>Das Raummodell dieses Hauses („…_s2_g1_hinterzimmer_innen“): jede Grundform und Stufe hat eigene, an ihrem Platz im Haus.</summary>
    private string WohnraumDatei(string kern) => $"{WohnraumHaus()}_{kern.ToLowerInvariant()}_innen";

    private bool WohnraumModellDa(string kern) => ResourceLoader.Exists($"res://assets/modelle/{WohnraumDatei(kern)}.glb");

    /// <summary>Ist das einer der Räume hier (Hinterzimmer, Bibliothek)?</summary>
    private static bool IstWohnraum(string? kennung) => kennung is not null && Wohnraeume.Any(r => r.Kennung == kennung);

    /// <summary>
    /// Gruppe der Raummodelle für Material und AO-Atlas („wohnhaus_mensch_s1_g1_hinterzimmer_innen“ → „wohnhaus_mensch_raeume_innen“);
    /// null für alle anderen Modelle (Stadtansicht.Neubau.cs, <see cref="NeubauGruppe"/>).
    /// </summary>
    private static string? WohnraumGruppe(string name) =>
        System.Text.RegularExpressions.Regex.Match(name, "^(.+)_s(\\d+)_g\\d+_([a-z]+)_innen$") is not { Success: true } m ? null
        : m.Groups[2].Value == "3" && m.Groups[3].Value == "bibliothek" && !m.Groups[1].Value.EndsWith("_elb") && !m.Groups[1].Value.EndsWith("_halbling") ? m.Groups[1].Value + "_dachraeume_innen"   // Steinhaus: Bibliothek im Dachgeschoss, eigener AO-Atlas (Elben, Halblinge: rechteckig im Obergeschoss)
        : m.Groups[1].Value + "_raeume_innen";

    /// <summary>Hat das Haus des Spielers diesen Raum (Kern), und steht es als Neubau? Mit <c>--neubauraeume</c> für Bilder auch ohne Anbau.</summary>
    private bool WohnraumGebaut(string kern)
    {
        if (!NeubauProbeAn || !WohnraumModellDa(kern) || _wohnhausAnsicht is not { } a || !_neubauHaeuser.TryGetValue(a.Kennung, out var haus)) return false;
        // Räume nur, wo es ein Raummodell zu genau diesem Haus gibt (WohnraumModellDa): Stufe 1 nur Grundform 1 (in 2–4 passen Wände
        // und Fenster nicht), Bürgerhaus und Steinhaus in allen vier Grundformen
        if (!IsInstanceValid(haus)) return false;
        if (NeubauRaeumeOption.Contains(kern)) return true;
        return _spielerKonto is { } konto && _simulation?.Wirtschaft is { } w && w.Wohnen.HatRaum(konto, kern);
    }

    /// <summary>Aus <see cref="WohnhausRaeumeAnlegen"/>: die Räume in die Raumleiste des Wohnhauses (nur zugänglich, wenn gebaut).</summary>
    private void WohnraeumeAnmelden(SonderbauAnsicht a)
    {
        if (!NeubauProbeAn) return;
        foreach (var (kennung, kern, titel) in Wohnraeume)
            a.Raeume.Add(new Raum(kennung, titel, Vector3.Zero, Vector3.Zero, () => WohnraumGebaut(kern)));
        GD.Print($"[wohnraeume] {a.Kennung}: Hinterzimmer und Bibliothek angemeldet (Modelle {string.Join(", ", Wohnraeume.Select(r => r.Kern + (WohnraumModellDa(r.Kern) ? " da" : " fehlt")))})");
    }

    /// <summary>Hover über einer Zimmertür (für den Mauszeiger: das Schloss wie an der Haustür).</summary>
    private bool IstWohnraumTuer(string? kennung) => kennung is not null && kennung.StartsWith(WohnraumTuer) && NeubauInnenAktiv;

    /// <summary>
    /// Klick auf eine Zimmertür (aus Hauptszene.RaumAngeklickt): die Hand greift die Klinke, dann die Blende, dahinter der Raumwechsel.
    /// Gibt false zurück, wenn die Kennung keine Zimmertür ist.
    /// </summary>
    public bool WohnraumTuerAngeklickt(string kennung)
    {
        if (!IstWohnraumTuer(kennung)) return false;
        var ziel = kennung[WohnraumTuer.Length..];
        if ((ziel == "Wohnstube" ? RaumWohnstube : Wohnraeume.Where(r => r.Kern == ziel).Select(r => r.Kennung).FirstOrDefault()) is not { } raum) return true;
        if (!Uebergangsblende.Aktiv || UebergangLaeuft) { if (!UebergangLaeuft) RaumWaehlen(raum); return true; }
        var schritte = new List<(string, Action)> { ("Raum", () => { _kameraSprung = true; try { RaumWaehlen(raum); } finally { _kameraSprung = false; } }) };
        Blende().Starten(UebergangRichtung.Betreten, raum, schritte, klinke: true);
        return true;
    }

    /// <summary>
    /// Zeigt einen der Räume im Haus (aus <see cref="NeubauInnenAbgleichen"/>): Knoten „InteriorRoot_&lt;Raum&gt;“ beim ersten Mal anlegen,
    /// den bisher gezeigten Innenraum ausschalten, Kamera auf den Blick des Raums. Ohne Modell bleibt es bei der Wohnstube.
    /// </summary>
    private void WohnraumAn(Node3D haus, string kennung)
    {
        var (_, kern, _) = Wohnraeume.First(r => r.Kennung == kennung);
        var name = "InteriorRoot_" + kern;
        var wurzel = haus.GetNodeOrNull<Node3D>(name);
        if (wurzel is null)
        {
            if (_wohnhausAnsicht is not { } a || Modell(WohnraumDatei(kern)) is not { } szene) { NeubauInnenAn(haus); return; }
            wurzel = szene.Instantiate<Node3D>();
            wurzel.Name = name;
            haus.AddChild(wurzel);
            NeubauEinkleiden(WohnraumDatei(kern), wurzel);
            WohnraumBeleben(a, wurzel);
        }
        if (_neubauInnen is not null && _neubauInnen != wurzel) NeubauInnenAus();
        _neubauInnen = wurzel;
        wurzel.Visible = true;
        wurzel.ProcessMode = ProcessModeEnum.Inherit;
        if (haus.FindChild("Scheiben", true, false) is Node3D scheiben) scheiben.Visible = false;
        if (_wohnhausAnsicht?.Innen is { } unten) unten.Visible = false; // die alte Wohnstube 200 m unter der Stadt wird nicht gebraucht
        _sonne.Visible = true;
        _raumKamera.Environment = null;
        _raumKamera.Fov = 66;
        if (wurzel.FindChild("Blick_Auge", true, false) is Node3D auge && wurzel.FindChild("Blick_Ziel", true, false) is Node3D ziel)
            _raumKamera.LookAtFromPosition(auge.GlobalPosition, ziel.GlobalPosition, Vector3.Up);
        _neubauBlickGrund = _raumKamera.GlobalTransform;
        (_neubauGier, _neubauNick, _neubauGierZiel, _neubauNickZiel) = (0, 0, 0, 0);
        GD.Print($"[wohnraeume] {kern} im Haus, Kamera {_raumKamera.GlobalPosition.ToString("0.00")}");
    }

    /// <summary>Licht, Feuer, Türen und Gegenstände eines Raums aus seinen Ankern (einmal je Raum, wie Wohnstube und Rathaus-Neubau).</summary>
    private void WohnraumBeleben(SonderbauAnsicht a, Node3D wurzel)
    {
        foreach (var knoten in wurzel.FindChildren("*", nameof(Node3D), true, false).OfType<Node3D>().ToList())
        {
            var name = knoten.Name.ToString();
            var lokal = wurzel.ToLocal(knoten.GlobalPosition);
            if (name.StartsWith("Licht_"))
                knoten.AddChild(ExtraText(knoten, "art") switch
                {
                    "Raum" => new OmniLight3D { LightColor = new Color(0.95f, 0.88f, 0.76f), LightEnergy = 1.1f, OmniRange = 7f, ShadowEnabled = false },
                    "Kerze" => new OmniLight3D { LightColor = new Color(1f, 0.78f, 0.50f), LightEnergy = 1.3f, OmniRange = 5f, ShadowEnabled = true },
                    _ => new OmniLight3D { LightColor = new Color(1.0f, 0.6f, 0.3f), LightEnergy = 2.6f, OmniRange = 6f, ShadowEnabled = true, OmniAttenuation = 1.1f },
                });
            else if (name.StartsWith("Feuer_"))
            {
                // Feuer_<Art>_<n>: Kerze, Ofen oder Kamin – brennt, solange man im Raum ist
                var art = name.Split('_') is { Length: >= 2 } t ? t[1] : "Kerze";
                var (vorgabe, groesse) = art switch { "Kerze" => (FeuerVorgabe.Kerze, 0.22f), "Ofen" => (FeuerVorgabe.Kamin, 0.7f), _ => (FeuerVorgabe.Kamin, 1.0f) };
                FlammeAnlegen(a, lokal - a.Mitte, vorgabe, groesse, () => _neubauInnen == wurzel, wurzel, art: art == "Kamin" ? Raumkamin() : null); // Kamin: ohne Rauch (er stiege durch den Rauchfang), sonst füllte der Schornsteinrauch des Freien den Raum
            }
            else if (name.StartsWith("Tuer_") && ExtraText(knoten, "ziel") is { Length: > 0 } ziel)
            {
                var b = ExtraZahlKomma(knoten, "breite", 0.9f);
                var h = ExtraZahlKomma(knoten, "hoehe", 2.0f);
                var groesse = ExtraText(knoten, "achse") == "x" ? new Vector3(0.3f, h, b) : new Vector3(b, h, 0.3f); // achse x: die Wand steht quer zur X-Achse
                var flaeche = Klickflaeche(WohnraumTuer + ziel, lokal, groesse, wurzel);
                flaeche.Disabled = false;
                a.Raumklicks.Add(flaeche);
            }
        }
        var gegenstaende = new List<(string Kennung, MeshInstance3D Teil, Label3D Schild)>();
        foreach (var teil in wurzel.FindChildren("Gegenstand_*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var name = teil.Name.ToString()["Gegenstand_".Length..];
            var kasten = teil.GetAabb();
            var mitte = wurzel.ToLocal(teil.ToGlobal(kasten.GetCenter()));
            var groesse = new Vector3(Mathf.Max(kasten.Size.X + 0.06f, 0.14f), Mathf.Max(kasten.Size.Y + 0.06f, 0.14f), Mathf.Max(kasten.Size.Z + 0.06f, 0.14f));
            var flaeche = Klickflaeche(NeubauGegenstand + name, mitte, groesse, wurzel);
            flaeche.Disabled = false;
            a.Raumklicks.Add(flaeche);
            var schild = new Label3D
            {
                Text = WohnraumGegenstandTitel.GetValueOrDefault(name, name), Position = mitte + new Vector3(0, groesse.Y / 2 + 0.14f, 0),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, PixelSize = 0.0017f, FontSize = 64, OutlineSize = 14,
                Modulate = new Color(1f, 0.93f, 0.72f), OutlineModulate = new Color(0.12f, 0.08f, 0.05f), Visible = false,
            };
            wurzel.AddChild(schild);
            gegenstaende.Add((NeubauGegenstand + name, teil, schild));
        }
        _neubauGlanz ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, AlbedoColor = new Color(1f, 0.82f, 0.42f, 0.30f),
        };
        a.Raumzustaende.Add((_, _) =>
        {
            if (!IsInstanceValid(wurzel) || _neubauInnen != wurzel) return;
            foreach (var (kennung, teil, schild) in gegenstaende)
            {
                var an = _unterDerMaus == kennung;
                if (schild.Visible == an) continue;
                schild.Visible = an;
                teil.MaterialOverlay = an ? _neubauGlanz : null;
            }
        });
        GD.Print($"[wohnraeume] {wurzel.Name}: {gegenstaende.Count} Gegenstände, Klickflächen im Raum: {a.Raumklicks.Count(k => IsInstanceValid(k) && wurzel.IsAncestorOf(k))}");
    }

    /// <summary>
    /// Aus <see cref="NeubauInnenBeleben"/>: die Zimmertüren der Wohnstube. Je Raum ein Knoten „Zimmertuer_&lt;Raum&gt;“ und ein Anker
    /// „Tuer_Wohnstube_&lt;Raum&gt;“; Tür und Klickfläche gibt es nur, solange das Haus den Raum hat (sonst ist dort Wand).
    /// </summary>
    private void WohnraumTuerenAnlegen(SonderbauAnsicht a, Node3D wurzel)
    {
        var tueren = new List<(string Kern, Node3D? Blatt, CollisionShape3D Klick)>();
        foreach (var (_, kern, _) in Wohnraeume)
        {
            if (wurzel.FindChild("Tuer_Wohnstube_" + kern, true, false) is not Node3D anker) continue;
            var b = ExtraZahlKomma(anker, "breite", 0.9f);
            var h = ExtraZahlKomma(anker, "hoehe", 2.0f);
            var groesse = ExtraText(anker, "achse") is "x" or "" ? new Vector3(0.3f, h, b) : new Vector3(b, h, 0.3f); // achse x: die Wand steht quer zur X-Achse (Stufe 1), sonst quer zur Z-Achse
            var flaeche = Klickflaeche(WohnraumTuer + kern, wurzel.ToLocal(anker.GlobalPosition), groesse, wurzel);
            a.Raumklicks.Add(flaeche);
            var blattName = ExtraText(anker, "blatt") is { Length: > 0 } geteilt ? geteilt : kern; // Bürgerhaus/Steinhaus: ein Türblatt „Zimmertuer_Gang“ für beide Räume
            tueren.Add((kern, wurzel.FindChild("Zimmertuer_" + blattName, true, false) as Node3D, flaeche));
        }
        GD.Print($"[wohnraeume] Wohnstube: {tueren.Count} Zimmertüren ({string.Join(", ", tueren.Select(t => t.Kern + (WohnraumGebaut(t.Kern) ? " offen" : " zu")))})");
        if (tueren.Count == 0) return;
        void Abgleichen()
        {
            foreach (var (kern, blatt, klick) in tueren)
            {
                var da = WohnraumGebaut(kern);
                // ein geteiltes Türblatt ist sichtbar, sobald einer seiner Räume gebaut ist
                if (blatt is not null) blatt.Visible = tueren.Any(t => t.Blatt == blatt && WohnraumGebaut(t.Kern));
                klick.Disabled = !da;
            }
        }
        Abgleichen();
        a.Raumzustaende.Add((_, _) => { if (IsInstanceValid(wurzel) && _neubauInnen == wurzel) Abgleichen(); });
    }
}
