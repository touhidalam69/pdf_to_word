using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PdfToWordOcr.Core;

public sealed class OcrClient
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string AnthropicVersion = "2023-06-01";
    private const int MaxRetries = 3;

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public OcrClient(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task<string> TranscribePageAsync(
        PageImage page,
        string model,
        string language,
        int pageNumber,
        CancellationToken ct,
        string? systemPromptOverride = null,
        int maxTokens = 8000)
    {
        var requestBody = new
        {
            model,
            max_tokens = maxTokens,
            system = systemPromptOverride ?? BuildSystemPrompt(language),
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = page.MediaType,
                                data = Convert.ToBase64String(page.Data),
                            },
                        },
                        new { type = "text", text = "Transcribe this page." },
                    },
                },
            },
        };

        for (var attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = JsonContent.Create(requestBody),
                };
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", AnthropicVersion);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (IsRetryableStatus(response.StatusCode))
                {
                    if (attempt < MaxRetries)
                    {
                        await DelayBeforeRetry(attempt + 1, ct).ConfigureAwait(false);
                        continue;
                    }

                    throw new OcrPageException(
                        pageNumber,
                        $"OCR failed for page {pageNumber} after {MaxRetries} retries: HTTP {(int)response.StatusCode}.");
                }

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
                return ExtractText(json);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt < MaxRetries)
                {
                    await DelayBeforeRetry(attempt + 1, ct).ConfigureAwait(false);
                    continue;
                }

                throw new OcrPageException(
                    pageNumber,
                    $"OCR failed for page {pageNumber} after {MaxRetries} retries.",
                    ex);
            }
        }
    }

    private static string BuildSystemPrompt(string language) =>
        $"You are an OCR engine for scanned {language} documents. Transcribe " +
        $"every character exactly as printed, in Unicode {language} script. " +
        "Preserve paragraph breaks (one blank line between paragraphs) and " +
        "reading order. Do NOT translate, correct, normalize, summarize, or " +
        "add commentary. Output ONLY the transcription. If the page is " +
        "blank, output nothing.";

    private static bool IsRetryableStatus(HttpStatusCode status)
    {
        var code = (int)status;
        return code is 429 or 529 or 500;
    }

    private static async Task DelayBeforeRetry(int attempt, CancellationToken ct)
    {
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 501));
        var backoff = TimeSpan.FromSeconds(Math.Pow(2, attempt));
        await Task.Delay(backoff + jitter, ct).ConfigureAwait(false);
    }

    private static string ExtractText(JsonElement root)
    {
        var sb = new StringBuilder();
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) &&
                    type.GetString() == "text" &&
                    block.TryGetProperty("text", out var text))
                {
                    sb.Append(text.GetString());
                }
            }
        }

        return sb.ToString();
    }
}
