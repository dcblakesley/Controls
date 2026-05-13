using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// Screenshot baseline comparison for the e2e suite.
/// </summary>
/// <remarks>
/// <para>
/// Baselines live under <c>Snapshots/&lt;TestClass&gt;-&lt;name&gt;.png</c> and are committed to
/// the repo. The diff is per-pixel against a small tolerance — passes through harmless
/// sub-pixel anti-aliasing without hiding actual regressions.
/// </para>
/// <para>
/// Set <c>UPDATE_SNAPSHOTS=1</c> in the environment to overwrite baselines with whatever the
/// current run produces. Use this after an intentional UI change; commit the updated PNGs.
/// </para>
/// <para>
/// On failure, an <c>-actual.png</c> and <c>-diff.png</c> are written alongside the baseline so
/// the reviewer can see what changed.
/// </para>
/// </remarks>
public static class VisualRegression
{
    /// <summary>Tolerance per pixel — Euclidean distance in RGB space, 0–442 range (sqrt(3 * 255²)).</summary>
    const int PerPixelTolerance = 8;

    /// <summary>Maximum fraction of pixels allowed to differ before the test fails.</summary>
    const double MaxDifferingPixelRatio = 0.01;

    /// <summary>Path to the shared <c>Snapshots/</c> folder under the test project source tree.</summary>
    public static string SnapshotsDirectory { get; } = LocateSnapshotsDirectory();

    public static bool UpdateMode =>
        Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1";

    public static void Assert(byte[] actualPng, string baselineName)
    {
        var baselinePath = Path.Combine(SnapshotsDirectory, baselineName + ".png");
        Directory.CreateDirectory(SnapshotsDirectory);

        // First run / explicit update: write the baseline and pass.
        if (UpdateMode || !File.Exists(baselinePath))
        {
            File.WriteAllBytes(baselinePath, actualPng);
            return;
        }

        using var actual = Image.Load<Rgba32>(actualPng);
        using var baseline = Image.Load<Rgba32>(baselinePath);

        if (actual.Width != baseline.Width || actual.Height != baseline.Height)
        {
            WriteFailureArtifacts(actualPng, null, baselinePath);
            throw new Xunit.Sdk.XunitException(
                $"Screenshot dimensions changed for {baselineName}: " +
                $"baseline {baseline.Width}x{baseline.Height} vs actual {actual.Width}x{actual.Height}. " +
                $"See {Path.GetFileNameWithoutExtension(baselinePath)}-actual.png.");
        }

        var (differingPixels, totalPixels, diffImage) = DiffPixels(baseline, actual);
        var ratio = (double)differingPixels / totalPixels;

        if (ratio > MaxDifferingPixelRatio)
        {
            var diffBytes = ToPngBytes(diffImage);
            WriteFailureArtifacts(actualPng, diffBytes, baselinePath);
            throw new Xunit.Sdk.XunitException(
                $"Screenshot regression for {baselineName}: " +
                $"{differingPixels:N0} of {totalPixels:N0} pixels differ ({ratio:P2}). " +
                $"Threshold {MaxDifferingPixelRatio:P0}. " +
                $"See {Path.GetFileNameWithoutExtension(baselinePath)}-actual.png + -diff.png.");
        }
    }

    static (int differingPixels, int totalPixels, Image<Rgba32> diff) DiffPixels(
        Image<Rgba32> baseline, Image<Rgba32> actual)
    {
        var diff = new Image<Rgba32>(baseline.Width, baseline.Height);
        var differing = 0;
        var total = baseline.Width * baseline.Height;

        for (var y = 0; y < baseline.Height; y++)
        {
            for (var x = 0; x < baseline.Width; x++)
            {
                var b = baseline[x, y];
                var a = actual[x, y];
                var dr = b.R - a.R;
                var dg = b.G - a.G;
                var db = b.B - a.B;
                var distanceSquared = dr * dr + dg * dg + db * db;
                if (distanceSquared > PerPixelTolerance * PerPixelTolerance)
                {
                    differing++;
                    diff[x, y] = new Rgba32(255, 0, 0); // red highlights for differing pixels
                }
                else
                {
                    // Faded baseline tone so reviewers can orient.
                    diff[x, y] = new Rgba32((byte)(b.R / 3), (byte)(b.G / 3), (byte)(b.B / 3));
                }
            }
        }

        return (differing, total, diff);
    }

    static byte[] ToPngBytes(Image image)
    {
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    static void WriteFailureArtifacts(byte[] actualPng, byte[]? diffPng, string baselinePath)
    {
        var dir = Path.GetDirectoryName(baselinePath)!;
        var stem = Path.GetFileNameWithoutExtension(baselinePath);
        File.WriteAllBytes(Path.Combine(dir, stem + "-actual.png"), actualPng);
        if (diffPng != null)
            File.WriteAllBytes(Path.Combine(dir, stem + "-diff.png"), diffPng);
    }

    static string LocateSnapshotsDirectory()
    {
        // Place Snapshots/ alongside the .csproj so baselines are tracked in source control,
        // not the bin output. Walk up from the test binary to find the project folder.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FormTesting.Client.E2ETests.csproj")))
            dir = dir.Parent;
        if (dir == null)
            throw new DirectoryNotFoundException("Could not locate FormTesting.Client.E2ETests project root for snapshot storage.");
        return Path.Combine(dir.FullName, "Snapshots");
    }
}
