using System.Text.Json;
using System.Text.Json.Serialization;
using Reichsgilde.Core.Wirtschaft;

namespace Reichsgilde.Core.Speichern;

/// <summary>
/// Schreibt und liest einen <see cref="Befehl"/> als Daten (Mehrspieler M2): <c>{"Befehl": "KarrenKaufen", "Werte": {"Betrieb": …, "Typ": …}}</c>.
///
/// Warum der Umschlag? Ein Feld „Befehl“ neben den Werten sagt, welche Art gemeint ist – die Werte selbst dürfen dann heißen, wie sie wollen
/// (mehrere Befehle haben ein Feld „Art“ oder „Typ“). Die Arten sucht der Konverter selbst: jede nicht abstrakte Klasse, die von
/// <see cref="Befehl"/> erbt. Ein neuer Befehl in <c>Befehle.cs</c> ist damit ohne weitere Arbeit speicherbar; der Test
/// „Befehlsbote: Jede Befehlsart lässt sich speichern und lesen“ prüft das für alle.
/// </summary>
internal sealed class BefehlKonverter : JsonConverter<Befehl>
{
    private static readonly Dictionary<string, Type> Arten = typeof(Befehl).Assembly.GetTypes()
        .Where(t => !t.IsAbstract && typeof(Befehl).IsAssignableFrom(t))
        .ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>Alle Befehlsarten, die der Konverter kennt (für den Test).</summary>
    internal static IReadOnlyCollection<Type> AlleArten => Arten.Values;

    // Nur der abstrakte Grundtyp: Die Werte eines konkreten Befehls schreibt der gewöhnliche Weg (sonst riefe der Konverter sich selbst auf).
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Befehl);

    public override Befehl Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var dokument = JsonDocument.ParseValue(ref reader);
        var wurzel = dokument.RootElement;
        if (wurzel.ValueKind != JsonValueKind.Object || !wurzel.TryGetProperty("Befehl", out var art) || art.ValueKind != JsonValueKind.String)
            throw new JsonException("Ein Befehl ohne Art („Befehl“).");
        var name = art.GetString()!;
        if (!Arten.TryGetValue(name, out var typ))
            throw new JsonException($"Einen Befehl „{name}“ kennt diese Programmversion nicht.");
        var befehl = wurzel.TryGetProperty("Werte", out var werte) ? werte.Deserialize(typ, options) : Activator.CreateInstance(typ);
        return befehl as Befehl ?? throw new JsonException($"Der Befehl „{name}“ hat keine Werte.");
    }

    public override void Write(Utf8JsonWriter writer, Befehl value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Befehl", value.GetType().Name);
        writer.WritePropertyName("Werte");
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
        writer.WriteEndObject();
    }
}
