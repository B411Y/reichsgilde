using Godot;
using Reichsgilde.Core.Bauen;
using Reichsgilde.Core.Kartenwerkstatt;

namespace Reichsgilde;

/// <summary>
/// Der gemeinsame Katalog der Felsgruppen (Felspinsel P5) – ein Teil der <c>partial class Kartenwerkstatt</c>: laden beim Start aus
/// <c>user://felsgruppen.json</c> (Startoption <c>--werkstatt-felskatalog=&lt;datei&gt;</c> für Klicktests: eine andere Datei), schreiben nach jeder
/// Änderung. Dazu die Tafel-Teile: „Als Gruppe ablegen“ und die Gruppennummer (wählen, verbinden, auflösen) bei gewählten Stücken, Umbenennen und
/// Löschen einer eigenen Vorlage beim Stempel und Streuen. Der Katalog gehört zu keiner Karte – Löschen ändert keine Karte und hat kein Rückgängig.
/// </summary>
public partial class Kartenwerkstatt
{
    /// <summary>Die Datei des Katalogs (gemeinsam für alle Karten, neben dem Ordner der eigenen Karten).</summary>
    private string _felsKatalogPfad = "user://" + Felskatalog.Dateiname;

    /// <summary>Die Kennung, deren Löschen schon einmal gedrückt wurde (der zweite Druck löscht).</summary>
    private string? _felsGruppeLoeschenBestaetigt;

    /// <summary>Lädt den Katalog (ohne Datei: nur die festen Gruppen). Fehler in einzelnen Einträgen stehen unten in der Leiste.</summary>
    private void FelsKatalogLaden(string? pfad = null)
    {
        if (pfad is { Length: > 0 }) _felsKatalogPfad = pfad;
        var fehler = new List<string>();
        var text = Godot.FileAccess.FileExists(_felsKatalogPfad) ? Godot.FileAccess.GetFileAsString(_felsKatalogPfad) : null;
        _stadt.FelsKatalog = Felskatalog.AusJson(text, fehler);
        if (_stadt.FelsGruppe is { } g && _stadt.FelsKatalog.Finde(g) is null) _stadt.FelsGruppe = null;
        if (fehler.Count > 0)
        {
            foreach (var f in fehler) GD.PushWarning(f);
            Melden($"{fehler[0]}{(fehler.Count > 1 ? $" (und {fehler.Count - 1} weitere)" : "")}", fehler: true);
        }
        GD.Print($"Felsgruppen: {_stadt.FelsKatalog.Eigene.Count} eigene aus {ProjectSettings.GlobalizePath(_felsKatalogPfad)}");
    }

    /// <summary>Übernimmt einen geänderten Katalog und schreibt ihn; false (mit Meldung), wenn die Datei nicht zu schreiben ist – dann bleibt der alte.</summary>
    private bool FelsKatalogSetzen(Felskatalog neu)
    {
        DirAccess.MakeDirRecursiveAbsolute(_felsKatalogPfad.GetBaseDir());
        using var datei = Godot.FileAccess.Open(_felsKatalogPfad, Godot.FileAccess.ModeFlags.Write);
        if (datei is null)
        {
            Melden($"Felsgruppen nicht gespeichert ({ProjectSettings.GlobalizePath(_felsKatalogPfad)}): {Godot.FileAccess.GetOpenError()}", fehler: true);
            return false;
        }
        datei.StoreString(neu.AlsJson());
        _stadt.FelsKatalog = neu;
        return true;
    }

    /// <summary>Legt die gewählten Stücke als eigene Gruppe ab; danach ist sie die Vorlage von Stempel und Streuen. Null bei einem Fehler (mit Meldung).</summary>
    private Felsgruppe? FelsGruppeAblegen(string name, IReadOnlyCollection<EntwurfFels> auswahl)
    {
        try
        {
            var neu = _stadt.FelsKatalog.Ablegen(name, _entwurf.FelsGruppenstuecke(auswahl), out var gruppe);
            if (!FelsKatalogSetzen(neu)) return null;
            _stadt.FelsGruppe = gruppe.Kennung;
            Melden($"Felsgruppe „{gruppe.Name}“ ({gruppe.Stuecke.Count} Stücke) abgelegt – sie steht für alle Karten unter Stempel und Streuen › Vorlage.");
            GD.Print($"Felsgruppe abgelegt: {gruppe.Kennung} „{gruppe.Name}“, {gruppe.Stuecke.Count} Stücke");
            return gruppe;
        }
        catch (ArgumentException ex)
        {
            Melden(ex.Message, fehler: true);
            return null;
        }
    }

    private bool FelsGruppeUmbenennen(string kennung, string name)
    {
        try
        {
            if (!FelsKatalogSetzen(_stadt.FelsKatalog.Umbenennen(kennung, name))) return false;
            Melden($"Felsgruppe umbenannt: „{_stadt.FelsKatalog.Finde(kennung)!.Name}“.");
            return true;
        }
        catch (ArgumentException ex)
        {
            Melden(ex.Message, fehler: true);
            return false;
        }
    }

    private bool FelsGruppeLoeschen(string kennung)
    {
        try
        {
            var name = _stadt.FelsKatalog.Finde(kennung)?.Name ?? kennung;
            if (!FelsKatalogSetzen(_stadt.FelsKatalog.Loeschen(kennung))) return false;
            if (_stadt.FelsGruppe == kennung) _stadt.FelsGruppe = null;
            Melden($"Felsgruppe „{name}“ aus dem Katalog gelöscht – gestempelte Stücke in den Karten bleiben.");
            return true;
        }
        catch (ArgumentException ex)
        {
            Melden(ex.Message, fehler: true);
            return false;
        }
    }

    /// <summary>Beim Stempel und Streuen mit einer eigenen Vorlage: Name ändern und Löschen (zweimal drücken).</summary>
    private void FelsVorlageVerwalten(Felsgruppe gruppe)
    {
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(zeile);
        var name = new LineEdit { Text = gruppe.Name, MaxLength = Felskatalog.NameMax, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Der Name der eigenen Gruppe (für alle Karten)" };
        zeile.AddChild(name);
        void Umbenennen()
        {
            if (name.Text.Trim() != gruppe.Name && FelsGruppeUmbenennen(gruppe.Kennung, name.Text)) EigenschaftenZeigen(neu: true);
        }
        name.TextSubmitted += _ => Umbenennen();
        Knopf(zeile, "Umbenennen", Umbenennen, "Gibt der eigenen Gruppe den Namen aus dem Feld (gilt für alle Karten)");
        var bestaetigt = _felsGruppeLoeschenBestaetigt == gruppe.Kennung;
        Knopf(zeile, bestaetigt ? "Wirklich löschen?" : "Löschen", () =>
            {
                if (_felsGruppeLoeschenBestaetigt != gruppe.Kennung)
                {
                    _felsGruppeLoeschenBestaetigt = gruppe.Kennung;
                    EigenschaftenZeigen(neu: true);
                    return;
                }
                _felsGruppeLoeschenBestaetigt = null;
                FelsGruppeLoeschen(gruppe.Kennung);
                EigenschaftenZeigen(neu: true);
            },
            "Nimmt die Gruppe aus dem Katalog (zweimal drücken; ohne Rückgängig). Schon gestempelte Stücke in den Karten bleiben.");
    }

    /// <summary>
    /// Bei gewählten Stücken der Abschnitt „Felsgruppe“: ablegen im Katalog (Name, ab zwei Stücken) und die Gruppennummer in der Karte – ganze Gruppe
    /// wählen, verbinden, auflösen (je ein Schritt für Strg+Z).
    /// </summary>
    private void FelsGruppeTafel(List<EntwurfFels> auswahl)
    {
        Hauptmenue.Text(_eigenschaften, "Felsgruppe", 15, Gold);
        var gruppen = _entwurf.FelsGruppenAnzahl(auswahl);
        var ganze = _entwurf.FelsGruppeVon(auswahl).Count;
        Hauptmenue.Text(_eigenschaften, gruppen == 0 ? "Die gewählten Stücke gehören zu keiner Gruppe."
                : $"Die gewählten Stücke gehören zu {gruppen} {(gruppen == 1 ? "Gruppe" : "Gruppen")} ({ganze} Stücke ganz).",
            12, Grau, umbruchBreite: Rechtsbreite - 30);
        var knoepfe = new HFlowContainer();
        knoepfe.AddThemeConstantOverride("h_separation", 6);
        knoepfe.AddThemeConstantOverride("v_separation", 4);
        _eigenschaften.AddChild(knoepfe);
        if (gruppen > 0)
        {
            Knopf(knoepfe, "Ganze Gruppe wählen", () => _stadt.FelsGruppeWaehlen(), "Wählt alle Stücke, die zur Gruppe der gewählten gehören (gestempelt mit „Stücke verbinden“ oder verbunden)");
            Knopf(knoepfe, "Gruppe auflösen", () =>
                {
                    if (_stadt.FelsAuswahlGruppeAufloesen()) EigenschaftenZeigen(neu: true);
                },
                "Die Stücke der Gruppe gehören nicht mehr zusammen; sie bleiben, wo sie sind (ein Schritt für Strg+Z)");
        }
        if (auswahl.Count > 1)
            Knopf(knoepfe, "Verbinden", () =>
                {
                    if (_stadt.FelsAuswahlVerbinden() > 0) EigenschaftenZeigen(neu: true);
                },
                "Die gewählten Stücke werden eine Gruppe: Ein Klick auf „Ganze Gruppe wählen“ wählt sie später zusammen (ein Schritt für Strg+Z)");
        if (auswahl.Count < Felskatalog.StueckeMin) return;
        var zeile = new HBoxContainer();
        zeile.AddThemeConstantOverride("separation", 6);
        _eigenschaften.AddChild(zeile);
        var name = new LineEdit { PlaceholderText = "Name der Gruppe", MaxLength = Felskatalog.NameMax, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            TooltipText = "Unter diesem Namen steht die Gruppe im Katalog (für alle Karten)" };
        zeile.AddChild(name);
        void Ablegen()
        {
            if (FelsGruppeAblegen(name.Text, auswahl) is not null) EigenschaftenZeigen(neu: true);
        }
        name.TextSubmitted += _ => Ablegen();
        var knopf = Knopf(zeile, $"Als Gruppe ablegen ({auswahl.Count})", Ablegen,
            $"Legt die gewählten Stücke als eigene Gruppe in den gemeinsamen Katalog (für alle Karten, höchstens {Felskatalog.StueckeMax} Stücke): Das größte Stück wird " +
            "die Mitte, Lage, Drehung, Größe, Achsen, Kippen, Rollen, Höhe, Einsinken und abweichendes Gestein kommen mit. Danach steht sie unter Stempel und Streuen › Vorlage " +
            "und lässt sich gedreht und gespiegelt setzen. Die Karte ändert sich dabei nicht.");
        knopf.Disabled = auswahl.Count > Felskatalog.StueckeMax;
    }
}
