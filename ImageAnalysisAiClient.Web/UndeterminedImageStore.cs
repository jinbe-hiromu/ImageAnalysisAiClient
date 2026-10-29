using OpenCvSharp;

namespace ImageAnalysisAiClient.Web;

internal static class UndeterminedImageStore
{
    private const string FolderName = "判定不能画像";

    public static bool IsUndeterminedResult(string response)
    {
        return response.Contains("判定不可", StringComparison.OrdinalIgnoreCase)
            || response.Contains("判定不能", StringComparison.OrdinalIgnoreCase);
    }

    public static void SaveSharpest(IReadOnlyList<byte[]> images)
    {
        if (images.Count == 0)
        {
            return;
        }

        var sharpestImage = images
            .Select(image => new { Image = image, Score = CalculateSharpness(image) })
            .MaxBy(candidate => candidate.Score);
        if (sharpestImage is null)
        {
            return;
        }

        var folderPath = Path.Combine(AppContext.BaseDirectory, FolderName);
        Directory.CreateDirectory(folderPath);
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
        File.WriteAllBytes(Path.Combine(folderPath, fileName), sharpestImage.Image);
    }

    private static double CalculateSharpness(byte[] image)
    {
        using var source = Cv2.ImDecode(image, ImreadModes.Grayscale);
        if (source.Empty())
        {
            return double.MinValue;
        }

        using var laplacian = new Mat();
        Cv2.Laplacian(source, laplacian, MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var standardDeviation);
        return standardDeviation.Val0 * standardDeviation.Val0;
    }
}
