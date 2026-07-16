using System.Runtime.CompilerServices;
using PDFtoImage;
using SkiaSharp;

namespace PdfToWordOcr.Core;

public sealed record PageImage(byte[] Data, string MediaType);

public static class PdfRasterizer
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const int JpegQuality = 88;

    // Batch-mode limits: Claude downscales past 1568px anyway, and the API caps
    // images at 5MB base64 (~3.5MB raw after the ~33% base64 inflation).
    private const int BatchMaxLongEdge = 1568;
    private const int BatchFallbackLongEdge = 1200;
    private const int BatchJpegQuality = 80;
    private const int BatchFallbackJpegQuality = 60;
    private const long BatchMaxEncodedBytes = (long)(3.5 * 1024 * 1024);

    public static int GetPageCount(byte[] pdfBytes) => Conversion.GetPageCount(pdfBytes);

    public static async IAsyncEnumerable<PageImage> RasterizeAsync(
        byte[] pdfBytes,
        int dpi,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var options = new RenderOptions(Dpi: dpi);
        await foreach (var bitmap in Conversion.ToImagesAsync(pdfBytes, options: options, cancellationToken: ct))
        {
            using (bitmap)
            {
                yield return Encode(bitmap);
            }
        }
    }

    private static PageImage Encode(SKBitmap bitmap)
    {
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        if (png.Size <= MaxImageBytes)
        {
            return new PageImage(png.ToArray(), "image/png");
        }

        using var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return new PageImage(jpeg.ToArray(), "image/jpeg");
    }

    /// <summary>
    /// Rasterizes the given 1-based pages into the work directory as size-capped
    /// JPEGs, skipping pages whose image file already exists. Yields each page
    /// number as it lands on disk so callers can report progress.
    /// </summary>
    public static async IAsyncEnumerable<int> RasterizeToDirectoryAsync(
        byte[] pdfBytes,
        JobWorkspace workspace,
        int dpi,
        IReadOnlyList<int> pageNumbers,
        Action<string>? log = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        workspace.EnsureDirectories();

        var pending = pageNumbers.Where(page => !workspace.HasPageImage(page)).ToArray();
        foreach (var alreadyDone in pageNumbers.Except(pending))
        {
            yield return alreadyDone;
        }

        if (pending.Length == 0)
        {
            yield break;
        }

        var options = new RenderOptions(Dpi: dpi);
        var zeroBasedIndices = pending.Select(page => page - 1);
        var position = 0;

        await foreach (var bitmap in Conversion.ToImagesAsync(pdfBytes, zeroBasedIndices, options: options, cancellationToken: ct))
        {
            var pageNumber = pending[position++];
            using (bitmap)
            {
                var data = EncodeForBatch(bitmap, pageNumber, log);
                await File.WriteAllBytesAsync(workspace.PageImagePath(pageNumber), data, ct).ConfigureAwait(false);
            }

            yield return pageNumber;
        }
    }

    private static byte[] EncodeForBatch(SKBitmap bitmap, int pageNumber, Action<string>? log)
    {
        var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
        using var capped = ResizeToLongEdge(bitmap, BatchMaxLongEdge, sampling);
        var source = capped ?? bitmap;

        using var jpeg = source.Encode(SKEncodedImageFormat.Jpeg, BatchJpegQuality);
        if (jpeg.Size <= BatchMaxEncodedBytes)
        {
            return jpeg.ToArray();
        }

        log?.Invoke($"Page {pageNumber}: {jpeg.Size / 1024} KB at quality {BatchJpegQuality}, re-encoding at quality {BatchFallbackJpegQuality}.");
        using var lowQuality = source.Encode(SKEncodedImageFormat.Jpeg, BatchFallbackJpegQuality);
        if (lowQuality.Size <= BatchMaxEncodedBytes)
        {
            return lowQuality.ToArray();
        }

        log?.Invoke($"Page {pageNumber}: still {lowQuality.Size / 1024} KB, shrinking to {BatchFallbackLongEdge}px long edge.");
        using var shrunk = ResizeToLongEdge(source, BatchFallbackLongEdge, sampling);
        using var final = (shrunk ?? source).Encode(SKEncodedImageFormat.Jpeg, BatchFallbackJpegQuality);
        return final.ToArray();
    }

    /// <summary>Downscales so the longest edge is at most maxLongEdge; null when no resize is needed.</summary>
    private static SKBitmap? ResizeToLongEdge(SKBitmap bitmap, int maxLongEdge, SKSamplingOptions sampling)
    {
        var longEdge = Math.Max(bitmap.Width, bitmap.Height);
        if (longEdge <= maxLongEdge)
        {
            return null;
        }

        var scale = (double)maxLongEdge / longEdge;
        var width = Math.Max(1, (int)Math.Round(bitmap.Width * scale));
        var height = Math.Max(1, (int)Math.Round(bitmap.Height * scale));
        return bitmap.Resize(new SKImageInfo(width, height), sampling);
    }
}
