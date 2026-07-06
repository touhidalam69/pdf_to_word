using System.Runtime.CompilerServices;
using PDFtoImage;
using SkiaSharp;

namespace PdfToWordOcr.Core;

public sealed record PageImage(byte[] Data, string MediaType);

public static class PdfRasterizer
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const int JpegQuality = 88;

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
}
