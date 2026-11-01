using Godot;

namespace Reichsgilde;

/// <summary>
/// Die Stufen der Fahrzeit für die Anzeige des Fahrwegs im Baumodus (Wunsch des Nutzers vom 01.11.2026): bis <see cref="GruenBis"/> Minuten grün
/// (alles im Rahmen), bis <see cref="GelbBis"/> gelb (Warnung), darüber rot (fast zu weit). Gemeint ist die Hinfahrt des Startkarrens. Die Farben
/// gehen über <see cref="Uebergang"/> Minuten ineinander über, damit der Verlauf auf dem Weg weich wirkt. Nur für die Darstellung – der Kern kennt
/// keine Stufen.
/// </summary>
public static class Fahrzeitstufen
{
    /// <summary>Bis zu so vielen Minuten ist eine Fahrt im Rahmen (grün).</summary>
    public const int GruenBis = 45;

    /// <summary>Bis zu so vielen Minuten ist sie eine Warnung (gelb), darüber fast zu weit (rot).</summary>
    public const int GelbBis = 90;

    /// <summary>So viele Minuten (je zur Hälfte vor und nach einer Schwelle) gehen zwei Farben ineinander über.</summary>
    public const float Uebergang = 8;

    // gedeckte, gemalte Farben (kein grelles Signal): Wiesengrün, Ocker, Ziegelrot
    public static readonly Color Gruen = new(0.38f, 0.70f, 0.27f);
    public static readonly Color Gelb = new(0.93f, 0.76f, 0.22f);
    public static readonly Color Rot = new(0.84f, 0.25f, 0.17f);

    /// <summary>Die Farbe nach der aufgelaufenen Fahrzeit (Minuten), mit weichem Übergang an den Schwellen.</summary>
    public static Color Farbe(float minuten)
    {
        var h = Uebergang / 2;
        if (minuten <= GruenBis - h) return Gruen;
        if (minuten < GruenBis + h) return Gruen.Lerp(Gelb, (minuten - (GruenBis - h)) / Uebergang);
        if (minuten <= GelbBis - h) return Gelb;
        if (minuten < GelbBis + h) return Gelb.Lerp(Rot, (minuten - (GelbBis - h)) / Uebergang);
        return Rot;
    }

    /// <summary>Die Farbe ohne Übergang (Ampel, Tafel): grün, gelb oder rot.</summary>
    public static Color Stufe(int minuten) => minuten <= GruenBis ? Gruen : minuten <= GelbBis ? Gelb : Rot;

    /// <summary>Das Wort zur Stufe für Tooltips.</summary>
    public static string Wort(int minuten) => minuten <= GruenBis ? "im Rahmen" : minuten <= GelbBis ? "weit" : "fast zu weit";
}

/// <summary>
/// Die Schilder und Ampeln des Fahrwegs (<see cref="Stadtansicht.FahrwegSchilder"/>, <see cref="Stadtansicht.FahrwegAmpeln"/>) über der Stadt, im Stil der
/// Oberfläche (kolorierter Holzschnitt): schwarzer Umriss, Schraffur im Schatten, die Handfarbe leicht neben den Linien.
///   • Schild am Ende jedes Weges: der Name des Platzes, „hin … · zurück … min“ mit dem Startkarren und darunter die Zeit mit dem schnellsten Karren.
///     Das Schild des besten Platzes kräftig mit goldenem Rand, die anderen blasser. Liegt ein Platz außerhalb des Bildes, steht sein Schild am
///     Rand, mit einer Spitze in seine Richtung.
///   • Ampel an jedem freien Bauort: ein kleines Siegel in der Farbe der Stufe, nah genug herangezoomt mit den Minuten darunter.
/// Fängt keine Maus ab und liegt unter allen Tafeln.
/// </summary>
public partial class FahrwegAnzeige : Control
{
    private static readonly Color Umriss = new(0.09f, 0.07f, 0.05f);
    private static readonly Color Papier = new(0.93f, 0.87f, 0.71f);
    private static readonly Color Tinte = new(0.13f, 0.10f, 0.07f);
    private static readonly Color Gold = new(0.86f, 0.66f, 0.24f);

    /// <summary>Ab dieser Größe des Bildausschnitts (Meter) zeigen die Ampeln keine Minuten mehr, nur noch ihre Farbe.</summary>
    private const float AmpelTextBisZoom = 150;

    private Stadtansicht? _stadt;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public void Verbinden(Stadtansicht stadt) => _stadt = stadt;

    /// <summary>Der Wert der Zeile „Fahrt zum Markt“ in Bautafel und Rückfrage: Minuten der Hinfahrt mit dem Startkarren und der Platz.</summary>
    public static string Kurz(Stadtansicht.FahrwegSchild s) => $"{s.Hin} min ({s.Markt})";

    /// <summary>Der Tooltip dazu: hin und zurück, der schnellste Karren, die Stufen und wie viele Plätze eingezeichnet sind.</summary>
    public static string Tipp(Stadtansicht.FahrwegSchild s, int plaetze)
    {
        var schnell = s.SchnellKarren != "" && s.SchnellKarren != s.Karren
            ? $" Mit dem {s.SchnellKarren}: hin {s.SchnellHin}, zurück {s.SchnellZurueck} min." : "";
        var andere = plaetze > 1 ? $" Die Wege zu allen {plaetze} Marktplätzen sind in der Stadt eingezeichnet, der kürzeste kräftig." : "";
        return $"So lange fährt der {s.Karren} (der Karren, mit dem die Werkstatt anfängt) von hier zum nächsten Marktplatz, {s.Markt}: " +
               $"hin {s.Hin}, zurück {s.Zurueck} min – {Fahrzeitstufen.Wort(s.Hin)}.{schnell}{andere}\n" +
               $"Grün bis {Fahrzeitstufen.GruenBis} min: im Rahmen · gelb bis {Fahrzeitstufen.GelbBis} min: weit · darüber rot: fast zu weit. " +
               "Je kürzer die Fahrt, desto öfter kommen Waren auf den Markt.";
    }

    public override void _Process(double delta)
    {
        // Die Kamera bewegt sich: jedes Bild neu zeichnen, solange es etwas zu zeigen gibt (das Zeichnen selbst ist billig)
        var zeigen = _stadt is { } s && (s.FahrwegSchilder.Count > 0 || s.FahrwegAmpeln.Count > 0);
        if (zeigen || _gezeigt) QueueRedraw();
        _gezeigt = zeigen;
    }

    private bool _gezeigt;

    public override void _Draw()
    {
        if (_stadt is not { } stadt) return;
        var schrift = Schriften.Text;
        var bild = new Rect2(Vector2.Zero, Size);

        // Ampeln zuerst (unter den Schildern); die unter dem Umriss nicht – dort steht der Weg selbst
        var mitText = stadt.KameraZoom < AmpelTextBisZoom;
        foreach (var ampel in stadt.FahrwegAmpeln)
        {
            if (stadt.FahrwegBauort is { } hier && new Vector2(hier.X - ampel.Ort.X, hier.Z - ampel.Ort.Z).Length() < 10) continue;
            if (stadt.Bildpunkt(ampel.Ort) is not { } p || !bild.Grow(-4).HasPoint(p)) continue;
            Siegel(p, mitText ? 7f : 5f, Fahrzeitstufen.Stufe(ampel.Minuten));
            if (mitText) Beschriftet(schrift, p + new Vector2(0, 19), $"{ampel.Minuten}", 12, Tinte, Papier);
        }

        // Schilder: die blassen zuerst, das des besten Platzes obenauf
        foreach (var schild in stadt.FahrwegSchilder.OrderBy(s => s.Bester))
            if (stadt.Bildpunkt(schild.Ort) is { } p)
                Schild(schrift, p, schild, bild);
    }

    /// <summary>Ein rundes Siegel: die Farbe um 1,5 Pixel versetzt, schwarzer Ring, zwei Schraffurstriche unten rechts.</summary>
    private void Siegel(Vector2 p, float r, Color farbe)
    {
        DrawCircle(p + new Vector2(1.5f, 1.5f), r, farbe);
        DrawArc(p, r, 0, Mathf.Tau, 20, Umriss, 2f, true);
        DrawLine(p + new Vector2(r * 0.15f, r * 0.75f), p + new Vector2(r * 0.75f, r * 0.15f), Umriss, 1.2f, true);
        DrawLine(p + new Vector2(-r * 0.25f, r * 0.85f), p + new Vector2(r * 0.85f, -r * 0.25f), Umriss, 1.2f, true);
    }

    /// <summary>Text mittig um <paramref name="mitte"/>, mit dunklem bzw. hellem Rand, damit er auf jedem Boden lesbar bleibt.</summary>
    private void Beschriftet(Font schrift, Vector2 mitte, string text, int groesse, Color farbe, Color rand)
    {
        var breite = schrift.GetStringSize(text, HorizontalAlignment.Left, -1, groesse).X;
        var ort = mitte + new Vector2(-breite / 2, groesse * 0.35f);
        DrawStringOutline(schrift, ort, text, HorizontalAlignment.Left, -1, groesse, 4, rand);
        DrawString(schrift, ort, text, HorizontalAlignment.Left, -1, groesse, farbe);
    }

    /// <summary>
    /// Ein Schild an einem Pfosten: oben der Name, darunter die Zeiten. Steht der Platz außerhalb des Bildes, rückt das Schild an den Rand und
    /// zeigt mit einer Spitze dorthin.
    /// </summary>
    private void Schild(Font schrift, Vector2 fuss, Stadtansicht.FahrwegSchild s, Rect2 bild)
    {
        var deckung = s.Bester ? 1f : 0.72f;
        var zeilen = new List<(string Text, int Groesse, Color Farbe)>
        {
            (s.Markt, s.Bester ? 17 : 15, Tinte),
            ($"{s.Karren}: hin {s.Hin} · zurück {s.Zurueck} min", 13, Tinte),
        };
        if (s.SchnellKarren != "" && s.SchnellKarren != s.Karren)
            zeilen.Add(($"{s.SchnellKarren}: hin {s.SchnellHin} · zurück {s.SchnellZurueck} min", 12, new Color(0.30f, 0.25f, 0.18f)));
        const float rand = 9, abstand = 3;
        var breite = zeilen.Select((z, i) => (i == 0 ? Schriften.Ueberschrift : schrift).GetStringSize(z.Text, HorizontalAlignment.Left, -1, z.Groesse).X + (i == 0 ? 14 : 0)).Max() + 2 * rand + 4;
        var hoehe = zeilen.Sum(z => z.Groesse + abstand) + 2 * rand - abstand;
        const float pfosten = 26;

        // Lage: über dem Fuß; außerhalb des Bildes an den Rand gerückt
        var drinnen = bild.Grow(-12).HasPoint(fuss);
        var oben = new Vector2(fuss.X - breite / 2, fuss.Y - pfosten - hoehe);
        var kasten = new Rect2(oben, breite, hoehe);
        var platz = bild.Grow(-12);
        var verschoben = new Vector2(
            Mathf.Clamp(kasten.Position.X, platz.Position.X, platz.End.X - breite),
            Mathf.Clamp(kasten.Position.Y, platz.Position.Y, platz.End.Y - hoehe));
        kasten.Position = verschoben;

        // Pfosten (nur, wenn der Platz im Bild ist) bzw. Spitze zum Platz
        if (drinnen && verschoben == oben)
        {
            var unten = new Vector2(fuss.X, kasten.End.Y);
            DrawLine(unten + new Vector2(1.5f, 0), fuss + new Vector2(1.5f, 0), new Color(0.45f, 0.30f, 0.16f, deckung), 4f);
            DrawLine(unten, fuss, new Color(Umriss, deckung), 1.6f);
        }
        else
        {
            var mitte = kasten.GetCenter();
            var richtung = (fuss - mitte).Normalized();
            var spitze = mitte + richtung * (Mathf.Min(breite, hoehe) / 2 + 16);
            var quer = new Vector2(-richtung.Y, richtung.X) * 8;
            var basis = mitte + richtung * (Mathf.Min(breite, hoehe) / 2 - 2);
            DrawColoredPolygon(new[] { basis + quer + new Vector2(1.5f, 1.5f), spitze + new Vector2(1.5f, 1.5f), basis - quer + new Vector2(1.5f, 1.5f) }, new Color(Papier, deckung));
            DrawPolyline(new[] { basis + quer, spitze, basis - quer }, new Color(Umriss, deckung), 2f, true);
        }

        // Papier um 2 Pixel neben dem Umriss (Handfarbe), schwarzer Rahmen, Schraffur am unteren und rechten Rand (Schatten)
        DrawRect(new Rect2(kasten.Position + new Vector2(2, 2), kasten.Size), new Color(Papier, 0.96f * deckung));
        DrawRect(kasten, new Color(Umriss, deckung), false, s.Bester ? 2.5f : 1.8f);
        if (s.Bester) DrawRect(kasten.Grow(-3.5f), new Color(Gold, 0.9f), false, 1.4f);
        for (var x = kasten.Position.X + 8; x < kasten.End.X - 2; x += 6)
            DrawLine(new Vector2(x, kasten.End.Y - 1), new Vector2(x + 4, kasten.End.Y - 5), new Color(Umriss, 0.55f * deckung), 1f);
        for (var y = kasten.Position.Y + 8; y < kasten.End.Y - 6; y += 6)
            DrawLine(new Vector2(kasten.End.X - 1, y), new Vector2(kasten.End.X - 5, y + 4), new Color(Umriss, 0.55f * deckung), 1f);

        // Farbmarke der Stufe links neben dem Namen, dann die Zeilen
        var y0 = kasten.Position.Y + rand;
        var marke = new Vector2(kasten.Position.X + rand + 4, y0 + zeilen[0].Groesse * 0.5f);
        Siegel(marke, 5, new Color(Fahrzeitstufen.Stufe(s.Hin), deckung));
        for (var i = 0; i < zeilen.Count; i++)
        {
            var (text, groesse, farbe) = zeilen[i];
            var x = kasten.Position.X + rand + (i == 0 ? 14 : 0);
            DrawString(i == 0 ? Schriften.Ueberschrift : schrift, new Vector2(x, y0 + groesse * 0.85f), text, HorizontalAlignment.Left, -1, groesse, new Color(farbe, deckung));
            y0 += groesse + abstand;
        }
    }
}
