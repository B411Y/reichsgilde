using Godot;
using Reichsgilde.Core.Bauen;

namespace Reichsgilde;

/// <summary>
/// Die Karte der Stadt aus <c>game/data/karten/greybox.json</c> (res://). Wie <c>wirtschaft.json</c> liest Godot die Datei,
/// der Kern bekommt nur den Text (<see cref="Stadtkarte.AusJson"/>).
///
/// Die Karte wird einmal gelesen und gemerkt: Die Stadtansicht braucht sie schon beim Aufbau (keine Bäume auf den
/// Bauzonen), die Hauptszene beim Erstellen oder Laden der Partie. Fehlt die Datei oder ist sie kaputt, meldet
/// <see cref="GD.PushError(string)"/> das laut – dann schlägt auch der Rauchtest der CI an –, und das Spiel läuft ohne Bauwesen weiter.
/// </summary>
public static class Stadtkarten
{
    /// <summary>Die Karte der Greybox-Stadt – die fest im Code gezeichnete Stadt. Köln folgt später.</summary>
    public const string GreyboxPfad = "res://data/karten/greybox.json";

    private static Stadtkarte? _greybox;
    private static bool _geladen;

    /// <summary>Die Karte der Greybox-Stadt, oder null (mit Fehlermeldung), wenn sie fehlt oder fehlerhaft ist.</summary>
    public static Stadtkarte? Greybox
    {
        get
        {
            if (_geladen) return _greybox;
            _geladen = true;
            try
            {
                var json = Godot.FileAccess.GetFileAsString(GreyboxPfad);
                if (string.IsNullOrEmpty(json))
                    GD.PushError($"Die Karte {GreyboxPfad} wurde nicht gefunden – ohne sie wird nicht gebaut.");
                else
                    _greybox = Stadtkarte.AusJson(json);
            }
            catch (KartenFehler fehler)
            {
                GD.PushError(fehler.Message);
            }
            return _greybox;
        }
    }
}
