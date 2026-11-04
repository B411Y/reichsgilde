using Godot;

namespace Reichsgilde;

/// <summary>
/// Hufklang der fahrenden Pferdekarren (Entscheidung des Nutzers, 04.11.2026): Die Hufe gehören dem Karren, nicht der Umgebung. Jeder fahrende
/// Pferdekarren in Hörweite bekommt einen Platz (höchstens <see cref="MaxGleichzeitig"/>, die nächsten zuerst); sein Klang kommt aus der Welt
/// (<see cref="AudioStreamPlayer3D"/> auf dem Bus UmgebungDraussen, Lautstärke und Seite nach Abstand und Lage zum Blickpunkt) und hängt am
/// <see cref="Untergrund"/> unter dem Karren: Pflaster gibt harte Hufe, jeder andere Weg gedämpfte. Steht der Karren oder verlässt die Hörweite,
/// blendet er aus. Reine Darstellung: gelesen wird nur, was die Stadtansicht ohnehin kennt (Stadtansicht.KarrenKlang.cs).
/// </summary>
public sealed class KlangKarren
{
    public enum Untergrund { Pflaster, Erde }

    /// <summary>So viele Karren sind zugleich zu hören.</summary>
    public const int MaxGleichzeitig = 3;

    /// <summary>Bis zu diesem Abstand (Meter) voll, darüber leiser (umgekehrt zur Entfernung).</summary>
    public const float VollBis = 10f;

    /// <summary>Pegel der Hufe im Nahbereich (dB, die Dateien liegen bei −26 LUFS): dezent wie die übrigen Rufe der Umgebung.</summary>
    public const float BasisDb = -8f;

    /// <summary>So lange (Sekunden) blenden Hufe ein und aus, auch beim Wechsel des Untergrunds.</summary>
    private const float Blende = 0.6f;

    public const string OrdnerPfad = "res://assets/audio/umgebung/karren/";

    public static string Pfad(Untergrund u) => OrdnerPfad + (u == Untergrund.Pflaster ? "hufe_pflaster" : "hufe_erde") + ".ogg";

    /// <summary>Hörweite (Meter vom Blickpunkt) je Zoom: nah am Bild Karren im Bild, weit herausgezoomt ein Stück mehr, aber nie die ganze Karte.</summary>
    public static float Reichweite(float zoom) => Mathf.Clamp(zoom * 0.9f, 30f, 90f);

    /// <summary>Weit herausgezoomt wird es leiser (bis −10 dB bei der größten Ansicht), wie die übrigen Rufe der Umgebung.</summary>
    public static float ZoomDb(float zoom) => -10f * Mathf.Clamp((zoom - 25f) / 115f, 0f, 1f);

    /// <summary>Ein Karren, der fahren und hörbar sein könnte: Schlüssel, Ort (Welt), Abstand zum Blickpunkt und der Untergrund unter ihm.</summary>
    public readonly record struct Kandidat(object Schluessel, string Name, Vector3 Ort, float Abstand, Untergrund Boden);

    private sealed class Platz
    {
        public required AudioStreamPlayer3D Spieler;
        public object? Schluessel;
        public Untergrund Boden, Spielt;
        public float Verstaerkung; // 0 bis 1
        public bool Gewollt;
        public string Name = "";
    }

    private readonly Platz[] _plaetze = new Platz[MaxGleichzeitig];
    private readonly Dictionary<Untergrund, AudioStream?> _toene = new();

    /// <summary>Der letzte gemeldete Zustand für das Protokoll und den Entwicklerreiter.</summary>
    public string Letzter { get; private set; } = "–";

    /// <summary>Wie viele Karren gerade zu hören sind (Verstärkung über Null).</summary>
    public int Hoerbar => _plaetze.Count(p => p is { Schluessel: not null } && p.Verstaerkung > 0.02f);

    /// <summary>Welche Karren (Namen) gerade Plätze haben, mit Untergrund: für die Hörprobe im Protokoll.</summary>
    public IEnumerable<(string Name, Untergrund Boden, float Verstaerkung)> Belegt =>
        _plaetze.Where(p => p is { Schluessel: not null }).Select(p => (p.Name, p.Boden, p.Verstaerkung));

    private AudioStream? Ton(Untergrund u)
    {
        if (_toene.TryGetValue(u, out var bekannt)) return bekannt;
        var pfad = Pfad(u);
        AudioStream? ton = ResourceLoader.Exists(pfad) ? GD.Load<AudioStream>(pfad) : null;
        if (ton is null) GD.PushError($"Klang fehlt: {pfad}");
        else if (ton is AudioStreamOggVorbis ogg) ogg.Loop = true; // fährt, solange der Karren fährt
        return _toene[u] = ton;
    }

    /// <summary>
    /// Jedes Bild: Plätze neu vergeben und nachführen. <paramref name="kandidaten"/> sind die fahrenden Pferdekarren (schon auf die Hörweite begrenzt),
    /// <paramref name="blickpunkt"/> der Boden unter dem Blick, <paramref name="hoerer"/> die Kamera (dort sitzt der Zuhörer): Der Klang steht um
    /// denselben Versatz neben dem Zuhörer, den der Karren zum Blickpunkt hat – so stimmen Abstand und Seite mit dem Bild.
    /// </summary>
    public void Aktualisieren(Node eltern, IReadOnlyList<Kandidat> kandidaten, Vector3 blickpunkt, Vector3 hoerer, float zoom, bool stumm, double delta)
    {
        foreach (var p in _plaetze) if (p is not null) p.Gewollt = false;

        if (!stumm)
        {
            // Wer schon einen Platz hat und noch hörbar ist, behält ihn; freie Plätze bekommen die nächsten
            var frei = new List<Kandidat>(kandidaten.OrderBy(k => k.Abstand));
            foreach (var p in _plaetze)
            {
                if (p?.Schluessel is null) continue;
                var k = frei.FindIndex(x => ReferenceEquals(x.Schluessel, p.Schluessel));
                if (k < 0) continue;
                p.Gewollt = true;
                Zuweisen(p, frei[k]);
                frei.RemoveAt(k);
            }
            for (var i = 0; i < _plaetze.Length && frei.Count > 0; i++)
            {
                if (_plaetze[i]?.Schluessel is not null) continue;
                var p = _plaetze[i] ??= new Platz { Spieler = NeuerSpieler(eltern, i) };
                p.Schluessel = frei[0].Schluessel;
                p.Name = frei[0].Name;
                p.Boden = frei[0].Boden;
                p.Verstaerkung = 0;
                p.Gewollt = true;
                Starten(p);
                Zuweisen(p, frei[0]);
                Melden($"{p.Name}: Hufe auf {Boden(p.Boden)} beginnen, Abstand {frei[0].Abstand:0} m");
                frei.RemoveAt(0);
            }
        }

        var schritt = (float)(delta / Blende);
        foreach (var p in _plaetze)
        {
            if (p is null || p.Schluessel is null) continue;
            // Wechselt der Untergrund, erst ausblenden, dann mit dem anderen Klang wieder ein
            var soll = p.Gewollt && p.Spielt == p.Boden ? 1f : 0f;
            p.Verstaerkung = Mathf.MoveToward(p.Verstaerkung, soll, schritt);
            if (p.Verstaerkung <= 0.001f)
            {
                if (!p.Gewollt)
                {
                    p.Spieler.Stop();
                    p.Schluessel = null;
                    continue;
                }
                if (p.Spielt != p.Boden) Starten(p);
            }
            p.Spieler.VolumeDb = BasisDb + ZoomDb(zoom) + (p.Verstaerkung <= 0.001f ? -80f : 20f * Mathf.Log(p.Verstaerkung) / Mathf.Log(10f));
            if (!p.Spieler.Playing && p.Spieler.Stream is not null) p.Spieler.Play();
        }

        void Zuweisen(Platz p, Kandidat k)
        {
            if (k.Boden != p.Boden)
            {
                Melden($"{p.Name}: Untergrund {Boden(p.Boden)} -> {Boden(k.Boden)}");
                p.Boden = k.Boden;
            }
            p.Spieler.GlobalPosition = hoerer + (k.Ort - blickpunkt);
        }
    }

    /// <summary>Den Klang des Untergrunds von einer zufälligen Stelle an spielen (zwei Karren klingen nicht gleich).</summary>
    private void Starten(Platz p)
    {
        p.Spielt = p.Boden;
        p.Spieler.Stream = Ton(p.Boden);
        if (p.Spieler.Stream is { } ton) p.Spieler.Play((float)(GD.Randf() * ton.GetLength()));
    }

    private static string Boden(Untergrund u) => u == Untergrund.Pflaster ? "Pflaster" : "Erde";

    private void Melden(string text)
    {
        Letzter = text;
        GD.Print($"[karrenklang] {text}");
    }

    private AudioStreamPlayer3D NeuerSpieler(Node eltern, int nummer)
    {
        var s = new AudioStreamPlayer3D
        {
            Name = $"Hufe{nummer + 1}", Bus = KlangDienst.BusUmgebungDraussen, MaxDistance = 100f, UnitSize = VollBis,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance, MaxPolyphony = 1, VolumeDb = -80f,
        };
        eltern.AddChild(s);
        return s;
    }

    /// <summary>Beim Verlassen der Stadt alles anhalten.</summary>
    public void Beenden()
    {
        foreach (var p in _plaetze)
        {
            if (p is null) continue;
            p.Spieler.Stop();
            p.Spieler.Stream = null;
            p.Spieler.QueueFree();
        }
        Array.Clear(_plaetze);
        _toene.Clear();
    }
}
