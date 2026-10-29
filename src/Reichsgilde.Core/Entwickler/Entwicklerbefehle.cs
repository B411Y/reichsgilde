using Reichsgilde.Core.Dynastie;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Entwickler;

/// <summary>
/// Ein Befehl des Entwicklerfensters („Trainer“): Er setzt Werte direkt, an den Regeln vorbei – zum Ausprobieren und Testen.
///
/// Es sind Befehle wie alle anderen (reine Daten, über <see cref="Mehrspieler.Befehlsbote"/>), damit sie im selben Tick wirken wie jeder andere
/// Befehl, im Spielstand stehen können und ein <see cref="Mehrspieler.Befehlsprotokoll"/> mit ihnen gleich abläuft. Sie wirken nur, solange
/// <see cref="Entwicklerhilfe.Erlaubt"/> an ist (sonst abgelehnt), kosten keine Aktionspunkte, und nach dem ersten, der wirkt, steht im
/// Spielstand für immer „mit Entwicklerhilfe“ (<see cref="Entwicklerhilfe.Benutzt"/>).
///
/// <c>Haus</c> ist der Name eines Hauses (<see cref="Konto.Inhaber"/>); null heißt: das Haus, das den Befehl schickt.
/// Personen stehen als Nummer (<see cref="Person.Nummer"/>), Betriebe als Name.
/// </summary>
public abstract record EntwicklerBefehl : Befehl;

// ── Haus ────────────────────────────────────────────────────────────────────

/// <summary>Gulden geben (positiv) oder nehmen (negativ) – ohne Buchung in der Jahresrechnung.</summary>
public sealed record EntwicklerGulden(double Betrag, string? Haus = null) : EntwicklerBefehl;

/// <summary>Aktionspunkte auf <paramref name="Anzahl"/> setzen; null füllt bis zum Höchstwert (<c>aktionspunkte.hoechstens</c>) auf.</summary>
public sealed record EntwicklerAktionspunkte(int? Anzahl = null, string? Haus = null) : EntwicklerBefehl;

/// <summary>Den Stand (Titel) setzen: Stufe ab 0 in <c>staende</c> – ohne Vermögen, Gebühr und Wohnhaus.</summary>
public sealed record EntwicklerStand(int Stand, string? Haus = null) : EntwicklerBefehl;

/// <summary>
/// Das Ansehen jetzt auf <paramref name="Wert"/> setzen. Das geht über einen festen Zuschlag (<see cref="Konto.EntwicklerAnsehen"/>):
/// Ändern sich danach Vermögen, Stand oder Schande, wandert das Ansehen von dort aus weiter.
/// </summary>
public sealed record EntwicklerAnsehen(double Wert, string? Haus = null) : EntwicklerBefehl;

/// <summary>
/// Die Meinung einer Person über das Haus (sein Oberhaupt) jetzt auf <paramref name="Wert"/> setzen (−100 … +100).
/// <paramref name="Von"/> ist eine <see cref="ICharakter.Kennung"/> („P12“, „R:Ulrich Wendelstein“). Es wird ein Grund „Entwicklerhilfe“
/// unter den Taten; er verblasst am Jahresende wie jede Tat.
/// </summary>
public sealed record EntwicklerMeinung(string Von, double Wert, string? Haus = null) : EntwicklerBefehl;

// ── Familie ─────────────────────────────────────────────────────────────────

/// <summary>Ein Kind kommt sofort zur Welt – vom Oberhaupt und seinem Ehepartner, ohne Ehepartner ein Kind des Oberhaupts allein.</summary>
public sealed record EntwicklerGeburt(Geschlecht Geschlecht, string? Haus = null) : EntwicklerBefehl;

/// <summary>
/// Das Oberhaupt heiratet sofort einen Heiratskandidaten (Nummer der Person; null: der erste passende) – ohne Zuneigung, Ansehen, Stand und
/// Kosten. Nur das Geschlecht muss passen, und das Oberhaupt darf nicht verheiratet sein.
/// </summary>
public sealed record EntwicklerHeirat(int? Kandidat = null, string? Haus = null) : EntwicklerBefehl;

/// <summary>Eine Person eines Hauses erkrankt an dieser Krankheit (Name aus <c>gesundheit.krankheiten</c> oder „Pest“); null heilt sie.</summary>
public sealed record EntwicklerKrankheit(int Person, string? Krankheit) : EntwicklerBefehl;

/// <summary>Eine Person stirbt sofort – mit Erbfolge, wenn es das Oberhaupt ist (ohne Erben erlischt das Haus).</summary>
public sealed record EntwicklerTod(int Person) : EntwicklerBefehl;

/// <summary>Einen Charakterzug (Kennung aus <c>charakterzuege</c>) hinzufügen oder entfernen. Beim Hinzufügen fällt sein Gegenteil weg.</summary>
public sealed record EntwicklerZug(int Person, string Zug, bool Hinzufuegen = true) : EntwicklerBefehl;

// ── Ereignisse ──────────────────────────────────────────────────────────────

/// <summary>Ein Einbruch ins Lager dieses Betriebs gelingt sofort (wie ein Diebstahl der Unterwelt, aber ohne Täter, Spur und Hehler).</summary>
public sealed record EntwicklerDiebstahl(string Betrieb) : EntwicklerBefehl;

/// <summary>Feuer in diesem Betrieb (wie eine gelungene Sabotage): Brandschaden, Stillstand, Reparatur – ohne Täter und Spur.</summary>
public sealed record EntwicklerFeuer(string Betrieb) : EntwicklerBefehl;

/// <summary>Feuer im Wohnhaus dieses Hauses (wie eine gelungene Brandstiftung): Schaden, Instandsetzung – ohne Täter und Spur.</summary>
public sealed record EntwicklerHausbrand(string? Haus = null) : EntwicklerBefehl;

// ── Wirtschaft ──────────────────────────────────────────────────────────────

/// <summary>Ware ins Lager des Betriebs legen (negativ: herausnehmen, höchstens was da ist) – ohne Kosten, auch über die Lagergrenze.</summary>
public sealed record EntwicklerWare(string Betrieb, string Ware, int Menge) : EntwicklerBefehl;

/// <summary>Alle Zutaten der Waren in Arbeit bis zur Lagergrenze je Ware auffüllen – ohne Kosten (nur Werkstätten mit Zutaten).</summary>
public sealed record EntwicklerZutaten(string Betrieb) : EntwicklerBefehl;
