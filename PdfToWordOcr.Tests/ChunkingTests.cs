using PdfToWordOcr.Core;

namespace PdfToWordOcr.Tests;

public class ChunkingTests
{
    [Theory]
    [InlineData(199, 1, 199)]
    [InlineData(200, 1, 200)]
    [InlineData(201, 2, 1)]
    public void ChunkBoundaries(int pageCount, int expectedChunks, int lastChunkSize)
    {
        var pages = Enumerable.Range(1, pageCount).ToArray();

        var chunks = BatchOcrClient.ChunkPages(pages);

        Assert.Equal(expectedChunks, chunks.Count);
        Assert.Equal(lastChunkSize, chunks[^1].Length);
        Assert.Equal(pages, chunks.SelectMany(chunk => chunk));
    }
}
