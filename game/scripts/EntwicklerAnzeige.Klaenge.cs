using Godot;

namespace Reichsgilde;

/// <summary>
/// Der Reiter <b>Klänge</b> des Entwicklerfensters: alle Klänge des Spiels zum Anhören, nur Darstellung (nichts geht an den Kern).
///   • <b>Busse</b>: je Regler-Bus aus <see cref="KlangDienst.Regelbusse"/> Prozent, dB, stumm und ein Pegel, der jedes Bild nachläuft.
///   • <b>Oberflächenklänge</b>: je <see cref="KlangOberflaeche.Ereignis"/> ein Knopf für den Klang wie im Spiel (Zufallswahl mit Tonhöhe und
///     Lautstärke der Tabelle) und je Variante ein Knopf für die einzelne Datei.
///   • <b>Umgebung</b> (Ton Schritt 4, <see cref="KlangUmgebung"/>): Eingaben (Uhr, Bezugsstunde, Zoom, Innenansicht, Pause), je Schicht Zweck,
///     Ziel- und Ist-Pegel mit Balken und ein Knopf zum Vorhören der Datei, je Rufart Fenster, Zeit bis zum nächsten Ruf, die Varianten und
///     „jetzt rufen“. Aufgefrischt nur bei Änderung (Texte auf 0,1 dB gerundet).
///   • <b>Weitere Dateien</b>: was sonst unter <c>res://assets/audio/</c> liegt (später Musik, Ansagen des Haushofmeisters), mit dem Bus
///     nach dem Ordner. So taucht jeder neue Klang hier auf, ohne dass dieser Reiter angefasst werden muss.
/// Es spielt immer nur ein Klang zur Zeit (ein eigener Spieler, auch in der Spielpause); die Knöpfe selbst klicken nicht (Hinweis <c>klang</c> = „still“).
/// </summary>
public sealed partial class EntwicklerAnzeige
{
    private const string AudioOrdner = "res://assets/audio/";
    private static readonly string[] AudioEndungen = { ".ogg", ".wav", ".mp3" };

    private AudioStreamPlayer _vorhoerer = null!;
    private readonly List<(int Bus, Label Wert, ProgressBar Pegel)> _busZeilen = new();
    private int _klaengeNummer = -1;
    private Label? _umgebungKopf;
    private readonly List<(int Schicht, Label Wert, ProgressBar Pegel)> _umgebungZeilen = new();
    private readonly List<(int Art, Label Wert)> _rufZeilen = new();

    private void KlaengeReiter()
    {
        var s = NeuerReiter("Klänge");
        _klaengeNummer = _reiter.GetTabCount() - 1;
        _vorhoerer = new AudioStreamPlayer { Name = "Vorhoerer", ProcessMode = ProcessModeEnum.Always };
        AddChild(_vorhoerer);
        Hauptmenue.Text(s, "Alle Klänge des Spiels zum Anhören, immer einer zur Zeit. Die Lautstärke gilt wie im Spiel: Regler und Stumm der Einstellungen (Reiter Ton) wirken mit.", 13, Grau, umbruchBreite: Breite - 60);

        var z = Zeile(s, "Abspielen", "Hält den Klang an, der gerade aus diesem Reiter spielt.");
        StillerKnopf(z, "Stopp", () => { _vorhoerer.Stop(); Melden("Gestoppt."); });

        Hauptmenue.Text(s, "Busse", 18, Gold);
        var raster = new GridContainer { Columns = 3 };
        raster.AddThemeConstantOverride("h_separation", 12);
        s.AddChild(raster);
        for (var i = 0; i < KlangDienst.Regelbusse.Length; i++)
        {
            var (bus, name, _) = KlangDienst.Regelbusse[i];
            var titel = Hauptmenue.Text(raster, $"{name} ({bus})", 15, Gold);
            titel.CustomMinimumSize = new Vector2(200, 0);
            var wert = Hauptmenue.Text(raster, "", 14, Grau);
            wert.CustomMinimumSize = new Vector2(260, 0);
            var pegel = new ProgressBar
            {
                MinValue = -60, MaxValue = 0, Value = -60, ShowPercentage = false, CustomMinimumSize = new Vector2(220, 14),
                SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = "Pegel des Busses jetzt (lauterer Kanal, −60 bis 0 dB).",
            };
            pegel.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.12f, 0.10f, 0.08f), BorderColor = Grau, BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1 });
            pegel.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Gruen });
            raster.AddChild(pegel);
            _busZeilen.Add((i, wert, pegel));
        }

        s.AddChild(new HSeparator());
        Hauptmenue.Text(s, "Oberflächenklänge (Bus Oberflaeche)", 18, Gold);
        var bekannt = new HashSet<string>();
        foreach (var ereignis in Enum.GetValues<KlangOberflaeche.Ereignis>())
        {
            var (dateien, db, hoehe) = KlangOberflaeche.Angaben(ereignis);
            var reihe = Zeile(s, ereignis.ToString(), $"Wie im Spiel: eine der {dateien.Count} Varianten zufällig, {db:0.#} dB, Tonhöhe {hoehe:0.##} mit leichter Schwankung.");
            var fluss = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            fluss.AddThemeConstantOverride("h_separation", 4);
            reihe.AddChild(fluss);
            StillerKnopf(fluss, "▶ wie im Spiel", () => Vorhoeren(KlangOberflaeche.Klang(ereignis), KlangDienst.BusOberflaeche, db, hoehe, $"{ereignis} (Zufallsvariante)"));
            foreach (var datei in dateien)
            {
                var pfad = KlangOberflaeche.Ordner + datei + ".ogg";
                bekannt.Add(pfad);
                StillerKnopf(fluss, datei, () => Vorhoeren(Laden(pfad), KlangDienst.BusOberflaeche, db, hoehe, datei + ".ogg"), pfad);
            }
        }

        UmgebungAbschnitt(s, bekannt);

        s.AddChild(new HSeparator());
        Hauptmenue.Text(s, "Weitere Dateien unter assets/audio/", 18, Gold);
        var weitere = AudioDateien(AudioOrdner).Where(p => !bekannt.Contains(p)).ToList();
        if (weitere.Count == 0)
            Hauptmenue.Text(s, "Keine weiteren – Oberflächenklänge und Umgebung stehen oben. Neue Dateien (Musik, Ansagen) erscheinen hier von selbst.", 14, Grau, umbruchBreite: Breite - 60);
        foreach (var gruppe in weitere.GroupBy(p => p[AudioOrdner.Length..p.LastIndexOf('/')]))
        {
            var bus = BusFuerOrdner(gruppe.Key);
            var reihe = Zeile(s, gruppe.Key, $"Bus {bus} (nach dem Ordner).");
            var fluss = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            fluss.AddThemeConstantOverride("h_separation", 4);
            reihe.AddChild(fluss);
            foreach (var pfad in gruppe)
            {
                var name = pfad[(pfad.LastIndexOf('/') + 1)..];
                StillerKnopf(fluss, name, () => Vorhoeren(Laden(pfad), bus, 0, 1, name), $"{pfad} · Bus {bus}");
            }
        }
    }

    /// <summary>Abschnitt „Umgebung“: Schichten und Rufarten aus <see cref="KlangUmgebungModell"/>, die Werte kommen in <see cref="KlaengeAuffrischen"/>.</summary>
    private void UmgebungAbschnitt(VBoxContainer s, HashSet<string> bekannt)
    {
        s.AddChild(new HSeparator());
        Hauptmenue.Text(s, "Umgebung (Bus Umgebung, Unterbusse UmgebungStadt/Draussen/RufNah/RufFern)", 18, Gold);
        Hauptmenue.Text(s, "Schleifen nach Uhr, Bezugsstunde (Licht) und Zoom, gedämpft in Räumen, im Dom und in der Pause; dazu Einzelrufe mit Mindestabstand. "
                           + "„▶“ spielt die Datei allein auf Bus Umgebung (ohne die Dämpfung der Unterbusse).", 13, Grau, umbruchBreite: Breite - 60);
        _umgebungKopf = Hauptmenue.Text(s, "", 13, Gold, umbruchBreite: Breite - 60);
        for (var i = 0; i < KlangUmgebungModell.Schichten.Length; i++)
        {
            var schicht = KlangUmgebungModell.Schichten[i];
            var pfad = KlangUmgebung.SchichtPfad(schicht.Name);
            bekannt.Add(pfad);
            var reihe = Zeile(s, schicht.Name, $"{schicht.Zweck}. Datei {pfad}, Grundpegel {schicht.GrundDb:0} dB, Bus {(schicht.Bus == KlangUmgebungModell.Bus.Stadt ? "UmgebungStadt" : "UmgebungDraussen")}.");
            var name = schicht.Name;
            StillerKnopf(reihe, "▶", () => Vorhoeren(Laden(pfad), KlangDienst.BusUmgebung, schicht.GrundDb, 1, name + ".ogg"), pfad);
            var pegel = new ProgressBar
            {
                MinValue = -60, MaxValue = 0, Value = -60, ShowPercentage = false, CustomMinimumSize = new Vector2(160, 14),
                SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = "Ist-Pegel der Schleife (−60 bis 0 dB, vor den Unterbussen).",
            };
            pegel.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = Gruen });
            reihe.AddChild(pegel);
            var wert = Hauptmenue.Text(reihe, schicht.Zweck, 13, Grau);
            wert.CustomMinimumSize = new Vector2(420, 0);
            _umgebungZeilen.Add((i, wert, pegel));
        }
        for (var i = 0; i < KlangUmgebungModell.Rufarten.Length; i++)
        {
            var art = KlangUmgebungModell.Rufarten[i];
            var nummer = i;
            var reihe = Zeile(s, "Ruf " + art.Name, $"{art.Zweck}. Mindestens {art.Mindest:0} s Abstand, im Mittel {art.Mittel:0} s (bei vollem Fenster), nah {art.NahDb:0} dB, fern {art.FernDb:0} dB, nur fern ab Zoom {art.FernAb:0}, bis Zoom {art.Bis:0}.");
            StillerKnopf(reihe, "jetzt rufen", () =>
            {
                if (KlangUmgebung.Aktiv is { Laeuft: true } u) { u.JetztRufen(nummer); Melden("Ruf: " + u.LetzterRuf); }
                else Melden("Die Umgebung läuft nicht (nur in der Partie).", false);
            });
            var fluss = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            fluss.AddThemeConstantOverride("h_separation", 4);
            reihe.AddChild(fluss);
            var wert = Hauptmenue.Text(fluss, art.Zweck, 13, Grau);
            wert.CustomMinimumSize = new Vector2(260, 0);
            foreach (var datei in KlangUmgebung.RufDateien(art))
            {
                var pfad = KlangUmgebung.RufPfad(datei);
                bekannt.Add(pfad);
                StillerKnopf(fluss, datei, () => Vorhoeren(Laden(pfad), KlangDienst.BusUmgebung, art.NahDb, 1, datei + ".ogg"), pfad);
            }
            _rufZeilen.Add((i, wert));
        }
    }

    /// <summary>Die Werte des Abschnitts Umgebung (nur bei Änderung neu geschrieben).</summary>
    private void UmgebungAuffrischen()
    {
        if (_umgebungKopf is null) return;
        if (KlangUmgebung.Aktiv is not { Laeuft: true } u)
        {
            const string aus = "Die Umgebung läuft nicht (nur in der Partie, nicht im Hauptmenü und nicht in der Kartenwerkstatt).";
            if (_umgebungKopf.Text != aus) _umgebungKopf.Text = aus;
            return;
        }
        var e = u.LetzteEingaben;
        var a = u.Letzte;
        var kopf = $"Uhr {(int)(e.UhrMinute / 60):00}:{(int)(e.UhrMinute % 60):00} · Bezugsstunde {e.Bezug:0.0} · {e.Jahreszeit} · Zoom {e.Zoom:0} (höchstens {e.ZoomGroesst:0}): "
                   + $"Nähe {a.Naehe:0.00}, Höhe {a.Hoehe:0.00} · {e.Innen}{(e.Pausiert ? " · Pause" : "")} · {e.TicksProSekunde:0.0} Spielminuten/s"
                   + $"{(a.Sprungblende ? " · SPRUNGBLENDE" : "")}\nStadt {a.StadtDb:0.0} dB, Tiefpass {a.StadtTiefpass:0} Hz · Draußen {a.DraussenDb:0.0} dB, Tiefpass {a.DraussenTiefpass:0} Hz"
                   + $" · Wetter-Faktoren {string.Join("/", KlangUmgebungModell.WetterFaktor.Select(f => f.ToString("0.#")))} · letzter Ruf: {u.LetzterRuf}";
        if (_umgebungKopf.Text != kopf) _umgebungKopf.Text = kopf;
        foreach (var (i, wert, pegel) in _umgebungZeilen)
        {
            var ziel = a.Ziel[i] > 0 ? KlangUmgebungModell.Schichten[i].GrundDb + 20 * MathF.Log10(a.Ziel[i]) : KlangUmgebungModell.AusDb;
            var text = $"Ziel {(ziel <= KlangUmgebungModell.StummDb ? "aus" : $"{ziel:0.0} dB")} · ist {(a.Db[i] <= KlangUmgebungModell.AusDb ? "aus" : $"{a.Db[i]:0.0} dB")} · Gewicht {a.Amplitude[i] * a.Amplitude[i]:0.00}";
            if (wert.Text != text) wert.Text = text;
            pegel.Value = Math.Clamp(a.Db[i], -60, 0);
        }
        foreach (var (i, wert) in _rufZeilen)
        {
            var g = KlangUmgebungModell.RufPlaner.FensterGewicht(i, e, a);
            var text = g > KlangUmgebungModell.RufSchwelle ? $"Fenster {g:0.00} · nächster in etwa {u.Planer.Rest(i) / g:0} s" : "außerhalb des Fensters oder der Zoomgrenze";
            if (wert.Text != text) wert.Text = text;
        }
    }

    /// <summary>Ein Knopf ohne eigenen Klick-Klang (sonst läge der Klick über dem Klang, den man hören will).</summary>
    private static void StillerKnopf(Container eltern, string text, Action aktion, string? tipp = null)
    {
        var knopf = Knopf(eltern, text, aktion, tipp);
        knopf.SetMeta("klang", "still");
    }

    private static AudioStream? Laden(string pfad) => ResourceLoader.Exists(pfad) ? ResourceLoader.Load<AudioStream>(pfad) : null;

    private void Vorhoeren(AudioStream? ton, string bus, float db, float hoehe, string name)
    {
        if (ton is null)
        {
            Melden($"{name}: Datei fehlt oder lässt sich nicht laden.", false);
            return;
        }
        _vorhoerer.Stop();
        _vorhoerer.Stream = ton;
        _vorhoerer.Bus = bus;
        _vorhoerer.VolumeDb = Math.Min(db, 0f);
        _vorhoerer.PitchScale = hoehe;
        _vorhoerer.Play();
        var laenge = ton is AudioStreamRandomizer ? "" : $", {ton.GetLength():0.00} s";
        var stumm = AudioServer.GetBusIndex(bus) is var index and >= 0 && AudioServer.IsBusMute(index) ? " – Bus ist stumm!" : "";
        Melden($"Spielt: {name} auf Bus {bus} ({db:0.#} dB{laenge}){stumm}", stumm == "");
    }

    /// <summary>Der Bus für eine Datei nach ihrem Ordner unter <c>assets/audio/</c> (wie ihn das Spiel später nehmen soll).</summary>
    private static string BusFuerOrdner(string ordner) => ordner switch
    {
        _ when ordner.StartsWith("musik") => KlangDienst.BusMusik,
        _ when ordner.Contains("stimmen") || ordner.Contains("ansagen") => KlangDienst.BusStimmen,
        _ when ordner.Contains("umgebung") => KlangDienst.BusUmgebung,
        _ when ordner.Contains("oberflaeche") => KlangDienst.BusOberflaeche,
        _ => KlangDienst.BusEffekte,
    };

    /// <summary>Alle Klangdateien unter dem Ordner, sortiert (über <see cref="ResourceLoader.ListDirectory"/>: findet sie auch im exportierten Spiel).</summary>
    private static List<string> AudioDateien(string ordner)
    {
        var ergebnis = new List<string>();
        foreach (var eintrag in ResourceLoader.ListDirectory(ordner))
        {
            if (eintrag.EndsWith('/')) ergebnis.AddRange(AudioDateien(ordner + eintrag));
            else if (AudioEndungen.Any(e => eintrag.EndsWith(e, StringComparison.OrdinalIgnoreCase))) ergebnis.Add(ordner + eintrag);
        }
        ergebnis.Sort(StringComparer.Ordinal);
        return ergebnis;
    }

    /// <summary>Busse und Pegel (jedes Bild, solange der Reiter offen ist; nur wenn er fertig gebaut ist).</summary>
    private void KlaengeAuffrischen()
    {
        if (!_klaengeFertig || _reiter.CurrentTab != _klaengeNummer) return;
        UmgebungAuffrischen();
        foreach (var (nummer, wert, pegel) in _busZeilen)
        {
            var index = AudioServer.GetBusIndex(KlangDienst.Regelbusse[nummer].Bus);
            if (index < 0)
            {
                wert.Text = "fehlt im Layout!";
                continue;
            }
            var stumm = AudioServer.IsBusMute(index);
            var text = $"{KlangDienst.Prozent(nummer)} % · {AudioServer.GetBusVolumeDb(index):0.0} dB{(stumm ? " · STUMM" : "")}";
            if (wert.Text != text) wert.Text = text;
            var spitze = Math.Max(AudioServer.GetBusPeakVolumeLeftDb(index, 0), AudioServer.GetBusPeakVolumeRightDb(index, 0));
            pegel.Value = Math.Clamp(float.IsFinite(spitze) ? spitze : -60, -60, 0);
        }
    }
}
