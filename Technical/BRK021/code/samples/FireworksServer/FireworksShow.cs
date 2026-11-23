using System.Text;
using System.Text.Json.Serialization;

namespace FireworksServer;

[JsonConverter(typeof(JsonStringEnumConverter<FireworksTheme>))]
public enum FireworksTheme
{
    Aurora,
    Cyberpunk,
    Ocean,
    Sunset,
    Celebration,
}

public sealed record FireworksThemeProfile(
    string Backdrop,
    string LaunchStyle,
    string ParticleStyle,
    string FinaleStyle,
    double Gravity,
    double Drag,
    double Drift,
    IReadOnlyList<string> AccentColors);

public sealed record FireworksCue(
    int DelayMilliseconds,
    double X,
    double Y,
    string PrimaryColor,
    string SecondaryColor,
    string Shape,
    double Scale,
    int Seed);

public sealed record FireworksShow(
    string Id,
    string Title,
    FireworksTheme Theme,
    int Intensity,
    string FinaleMessage,
    FireworksThemeProfile Profile,
    IReadOnlyList<FireworksCue> Cues);

public sealed record FireworksSettings(string DashboardUrl);

public sealed class ShowState
{
    private FireworksShow? _latest;

    public FireworksShow? Latest => Volatile.Read(ref _latest);

    public void SetLatest(FireworksShow show) => Volatile.Write(ref _latest, show);
}

public static class FireworksShowFactory
{
    private sealed record ChoreographyProfile(
        FireworksThemeProfile Visuals,
        string[] Shapes,
        string TargetPattern,
        int BaseCueCount,
        int CuesPerIntensity,
        int IntroDelay,
        int CueInterval,
        int FinaleInterval,
        double MinimumScale,
        double FinaleScale);

    private static readonly IReadOnlyDictionary<FireworksTheme, ChoreographyProfile> Profiles =
        new Dictionary<FireworksTheme, ChoreographyProfile>
        {
            [FireworksTheme.Aurora] = new(
                new("aurora", "drift", "curtain", "bloom", .018, .991, .055,
                    ["#7df9ff", "#34d399", "#8b5cf6", "#f0abfc"]),
                ["curtain", "wave", "willow"],
                "flow",
                BaseCueCount: 6,
                CuesPerIntensity: 3,
                IntroDelay: 760,
                CueInterval: 350,
                FinaleInterval: 150,
                MinimumScale: .82,
                FinaleScale: 1.18),
            [FireworksTheme.Cyberpunk] = new(
                new("cyber-city", "rail", "neon", "glitch", .028, .976, 0,
                    ["#ff2bd6", "#00f5ff", "#faff00", "#7c3aed"]),
                ["diamond", "polygon", "cross", "circuit"],
                "lanes",
                BaseCueCount: 8,
                CuesPerIntensity: 4,
                IntroDelay: 620,
                CueInterval: 225,
                FinaleInterval: 75,
                MinimumScale: .72,
                FinaleScale: 1.12),
            [FireworksTheme.Ocean] = new(
                new("ocean", "bubble", "ripple", "tide", .012, .988, .025,
                    ["#38bdf8", "#0ea5e9", "#2dd4bf", "#e0f2fe"]),
                ["ripple", "bubble", "ring", "wave"],
                "swell",
                BaseCueCount: 7,
                CuesPerIntensity: 3,
                IntroDelay: 780,
                CueInterval: 325,
                FinaleInterval: 125,
                MinimumScale: .82,
                FinaleScale: 1.2),
            [FireworksTheme.Sunset] = new(
                new("sunset", "comet", "ember", "horizon", .042, .982, .012,
                    ["#fb7185", "#f97316", "#fbbf24", "#c084fc"]),
                ["fan", "comet", "willow", "sunburst"],
                "fan",
                BaseCueCount: 6,
                CuesPerIntensity: 3,
                IntroDelay: 720,
                CueInterval: 385,
                FinaleInterval: 145,
                MinimumScale: .78,
                FinaleScale: 1.22),
            [FireworksTheme.Celebration] = new(
                new("festival", "salvo", "confetti", "banner", .038, .972, .018,
                    ["#ff3366", "#ffd166", "#06d6a0", "#4cc9f0", "#f8fafc"]),
                ["confetti", "streamer", "star", "pop"],
                "party",
                BaseCueCount: 10,
                CuesPerIntensity: 5,
                IntroDelay: 520,
                CueInterval: 175,
                FinaleInterval: 55,
                MinimumScale: .68,
                FinaleScale: 1.08),
        };

    public static FireworksShow Create(
        string title,
        FireworksTheme theme,
        int intensity,
        string finaleMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(finaleMessage);

        if (intensity is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be between 1 and 5.");
        }

        string normalizedTitle = title.Trim();
        string normalizedFinale = finaleMessage.Trim();
        var random = new Random(CreateStableSeed(normalizedTitle, theme, intensity, normalizedFinale));
        ChoreographyProfile profile = Profiles[theme];
        int cueCount = profile.BaseCueCount + (intensity * profile.CuesPerIntensity);
        var cues = new List<FireworksCue>(cueCount);

        for (int i = 0; i < cueCount; i++)
        {
            bool finale = i >= cueCount - intensity;
            int delay = finale
                ? profile.IntroDelay + ((cueCount - intensity) * profile.CueInterval) +
                    ((i - cueCount + intensity) * profile.FinaleInterval)
                : profile.IntroDelay + (i * profile.CueInterval);
            (double x, double y) = CreateTarget(profile.TargetPattern, i, cueCount, random);

            cues.Add(new FireworksCue(
                DelayMilliseconds: delay,
                X: x,
                Y: y,
                PrimaryColor: Pick(profile.Visuals.AccentColors, random),
                SecondaryColor: Pick(profile.Visuals.AccentColors, random),
                Shape: profile.Shapes[random.Next(profile.Shapes.Length)],
                Scale: (finale ? profile.FinaleScale : profile.MinimumScale) + (random.NextDouble() * .42),
                Seed: random.Next()));
        }

        return new FireworksShow(
            Id: Guid.NewGuid().ToString("N"),
            Title: normalizedTitle,
            Theme: theme,
            Intensity: intensity,
            FinaleMessage: normalizedFinale,
            Profile: profile.Visuals,
            Cues: cues);
    }

    public static string[] GetPalette(FireworksTheme theme) => [.. Profiles[theme].Visuals.AccentColors];

    private static string Pick(IReadOnlyList<string> values, Random random) => values[random.Next(values.Count)];

    private static (double X, double Y) CreateTarget(
        string pattern,
        int index,
        int cueCount,
        Random random)
    {
        double progress = cueCount == 1 ? 0 : (double)index / (cueCount - 1);
        return pattern switch
        {
            "flow" => (
                .1 + (random.NextDouble() * .8),
                .13 + (Math.Sin(progress * Math.PI * 3) + 1) * .08 + random.NextDouble() * .08),
            "lanes" => (
                .16 + ((index % 5) * .17) + ((random.NextDouble() - .5) * .025),
                .14 + ((index % 3) * .11) + random.NextDouble() * .035),
            "swell" => (
                .1 + (progress * .8),
                .2 + (Math.Sin(progress * Math.PI * 2) * .1) + random.NextDouble() * .055),
            "fan" => (
                .12 + (((index * 3) % 8) * .108) + ((random.NextDouble() - .5) * .025),
                .2 + Math.Abs(.5 - progress) * .2 + random.NextDouble() * .045),
            "party" => (
                .08 + (random.NextDouble() * .84),
                .1 + (random.NextDouble() * .42)),
            _ => throw new InvalidOperationException($"Unknown target pattern '{pattern}'."),
        };
    }

    private static int CreateStableSeed(
        string title,
        FireworksTheme theme,
        int intensity,
        string finaleMessage)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{title}\n{theme}\n{intensity}\n{finaleMessage}");
        uint hash = 2166136261;
        foreach (byte value in bytes)
        {
            hash = (hash ^ value) * 16777619;
        }

        return unchecked((int)hash);
    }
}
