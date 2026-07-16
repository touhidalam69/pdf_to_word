using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PdfToWordOcr.Core;

public sealed record BatchCounts(int Processing, int Succeeded, int Errored, int Canceled, int Expired);

public sealed record BatchStatus(string ProcessingStatus, BatchCounts Counts, string? ResultsUrl);

/// <summary>
/// Client for the Anthropic Message Batches API. Request bodies are written
/// streaming to a temp file (one page image in memory at a time) and results
/// are consumed line-by-line from the JSONL stream — a 1000-page book never
/// lives in memory at once.
/// </summary>
public sealed class BatchOcrClient
{
    private const string BatchesUrl = "https://api.anthropic.com/v1/messages/batches";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxCreateAttempts = 5;
    private const int MaxConsecutivePollFailures = 10;
    private const int MaxTokensPerPage = 4096;

    /// <summary>Requests per batch. API caps are 100k requests / 256 MB; 200 keeps uploads small.</summary>
    public const int ChunkSize = 200;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public BatchOcrClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public static IReadOnlyList<int[]> ChunkPages(IReadOnlyList<int> pageNumbers, int chunkSize = ChunkSize) =>
        pageNumbers.Chunk(chunkSize).ToArray();

    /// <summary>Submits one chunk of pages as a batch and returns the API batch id.</summary>
    public async Task<string> SubmitBatchAsync(
        JobWorkspace workspace,
        IReadOnlyList<int> pageNumbers,
        string model,
        string prompt,
        CancellationToken ct)
    {
        var bodyFile = Path.Combine(Path.GetTempPath(), $"PdfToWordOcr_batch_{Guid.NewGuid():N}.json");
        try
        {
            await WriteRequestBodyAsync(bodyFile, workspace, pageNumbers, model, prompt, ct).ConfigureAwait(false);

            for (var attempt = 1; ; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                using var request = new HttpRequestMessage(HttpMethod.Post, BatchesUrl);
                await using var bodyStream = File.OpenRead(bodyFile);
                request.Content = new StreamContent(bodyStream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                AddHeaders(request);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
                {
                    if (attempt >= MaxCreateAttempts)
                    {
                        throw new HttpRequestException(
                            $"Batch creation failed after {MaxCreateAttempts} attempts: HTTP {(int)response.StatusCode}.");
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct).ConfigureAwait(false);
                    continue;
                }

                response.EnsureSuccessStatusCode();

                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
                return json.RootElement.GetProperty("id").GetString()
                    ?? throw new InvalidOperationException("Batch creation response had no id.");
            }
        }
        finally
        {
            File.Delete(bodyFile);
        }
    }

    public async Task<BatchStatus> GetStatusAsync(string batchId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BatchesUrl}/{batchId}");
        AddHeaders(request);

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
        return ParseStatus(json.RootElement);
    }

    /// <summary>Polls every minute until the batch has ended; transient poll failures are tolerated.</summary>
    public async Task<BatchStatus> PollUntilEndedAsync(
        string batchId,
        Action<BatchCounts>? onPoll,
        CancellationToken ct)
    {
        var consecutiveFailures = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            BatchStatus status;
            try
            {
                status = await GetStatusAsync(batchId, ct).ConfigureAwait(false);
                consecutiveFailures = 0;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                && !ct.IsCancellationRequested)
            {
                if (++consecutiveFailures >= MaxConsecutivePollFailures)
                {
                    throw;
                }

                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
                continue;
            }

            onPoll?.Invoke(status.Counts);

            if (status.ProcessingStatus == "ended")
            {
                return status;
            }

            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Streams the batch results JSONL and writes each page's outcome into the
    /// work directory: success/blank → ocr text file, everything else →
    /// failures.txt. Re-collecting is idempotent.
    /// </summary>
    public async Task CollectResultsAsync(
        JobWorkspace workspace,
        string batchId,
        string? resultsUrl,
        string language,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, resultsUrl ?? $"{BatchesUrl}/{batchId}/results");
        AddHeaders(request);

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var result = BatchResultProcessor.ProcessLine(line, language);
            var pageNumber = JobWorkspace.TryParsePageNumber(result.CustomId);
            if (pageNumber is null)
            {
                workspace.Log($"Skipping result with unrecognized custom_id '{result.CustomId}'.");
                continue;
            }

            switch (result.Kind)
            {
                case BatchResultKind.Succeeded:
                    workspace.WriteOcrText(pageNumber.Value, result.Text);
                    break;
                case BatchResultKind.Blank:
                    workspace.WriteOcrText(pageNumber.Value, string.Empty);
                    break;
                default:
                    workspace.AppendFailure(result.CustomId, result.FailureReason ?? "unknown");
                    workspace.Log($"Page {pageNumber} failed: {result.FailureReason}.");
                    break;
            }
        }
    }

    private static async Task WriteRequestBodyAsync(
        string bodyFile,
        JobWorkspace workspace,
        IReadOnlyList<int> pageNumbers,
        string model,
        string prompt,
        CancellationToken ct)
    {
        await using var fileStream = File.Create(bodyFile);
        await using var writer = new Utf8JsonWriter(fileStream);

        writer.WriteStartObject();
        writer.WriteStartArray("requests");

        foreach (var pageNumber in pageNumbers)
        {
            ct.ThrowIfCancellationRequested();

            writer.WriteStartObject();
            writer.WriteString("custom_id", JobWorkspace.PageStem(pageNumber));
            writer.WriteStartObject("params");
            writer.WriteString("model", model);
            writer.WriteNumber("max_tokens", MaxTokensPerPage);
            writer.WriteNumber("temperature", 0);
            writer.WriteStartArray("messages");
            writer.WriteStartObject();
            writer.WriteString("role", "user");
            writer.WriteStartArray("content");

            writer.WriteStartObject();
            writer.WriteString("type", "image");
            writer.WriteStartObject("source");
            writer.WriteString("type", "base64");
            writer.WriteString("media_type", "image/jpeg");
            var imageBytes = await File.ReadAllBytesAsync(workspace.PageImagePath(pageNumber), ct).ConfigureAwait(false);
            writer.WriteBase64String("data", imageBytes);
            writer.WriteEndObject();
            writer.WriteEndObject();

            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", prompt);
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();

            await writer.FlushAsync(ct).ConfigureAwait(false);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        await writer.FlushAsync(ct).ConfigureAwait(false);
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", AnthropicVersion);
    }

    private static BatchStatus ParseStatus(JsonElement root)
    {
        var counts = new BatchCounts(0, 0, 0, 0, 0);
        if (root.TryGetProperty("request_counts", out var rc))
        {
            counts = new BatchCounts(
                GetCount(rc, "processing"),
                GetCount(rc, "succeeded"),
                GetCount(rc, "errored"),
                GetCount(rc, "canceled"),
                GetCount(rc, "expired"));
        }

        var resultsUrl = root.TryGetProperty("results_url", out var url) && url.ValueKind == JsonValueKind.String
            ? url.GetString()
            : null;

        return new BatchStatus(
            root.GetProperty("processing_status").GetString() ?? "unknown",
            counts,
            resultsUrl);
    }

    private static int GetCount(JsonElement counts, string name) =>
        counts.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}
