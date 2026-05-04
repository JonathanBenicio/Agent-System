using AgenticSystem.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AgenticSystem.Core.Tools;

public class DateTimeTool : ITool
{
    public string Id => "datetime";
    public string Name => "DateTime Tool";
    public string Description => "Fornece data, hora, fuso horário e cálculos temporais.";
    public ToolCategory Category => ToolCategory.Calendar;
    public bool RequiresAuth => false;

    public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var timezone = input.Parameters.TryGetValue("timezone", out var tz) ? tz?.ToString() : "UTC";

        return input.Action.ToLowerInvariant() switch
        {
            "now" => Task.FromResult(ToolResult.Ok(new
            {
                utc = now.ToString("O"),
                local = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(now, timezone ?? "UTC").ToString("O"),
                timezone,
                dayOfWeek = now.DayOfWeek.ToString()
            })),
            "diff" => CalculateDiff(input, now),
            _ => Task.FromResult(ToolResult.Ok(new
            {
                utc = now.ToString("O"),
                iso = now.ToString("yyyy-MM-dd HH:mm:ss"),
                unix = new DateTimeOffset(now).ToUnixTimeSeconds()
            }))
        };
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    private static Task<ToolResult> CalculateDiff(ToolInput input, DateTime now)
    {
        if (input.Parameters.TryGetValue("date", out var dateObj) &&
            DateTime.TryParse(dateObj?.ToString(), out var target))
        {
            var diff = target - now;
            return Task.FromResult(ToolResult.Ok(new
            {
                days = Math.Abs(diff.Days),
                hours = Math.Abs((int)diff.TotalHours),
                isPast = diff.TotalSeconds < 0
            }));
        }
        return Task.FromResult(ToolResult.Fail("Parâmetro 'date' inválido ou ausente."));
    }
}

public class HttpTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpTool> _logger;

    public HttpTool(HttpClient httpClient, ILogger<HttpTool> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public string Id => "http";
    public string Name => "HTTP Client Tool";
    public string Description => "Executa requisições HTTP (GET/POST) para APIs externas.";
    public ToolCategory Category => ToolCategory.Api;
    public bool RequiresAuth => false;

    public async Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!input.Parameters.TryGetValue("url", out var urlObj) || urlObj is not string url || string.IsNullOrWhiteSpace(url))
            return ToolResult.Fail("Parâmetro 'url' obrigatório.");

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "https" && uri.Scheme != "http"))
            return ToolResult.Fail("URL inválida. Use http:// ou https://.");

        var client = _httpClient;

        try
        {
            var method = input.Action.ToUpperInvariant() switch
            {
                "POST" => HttpMethod.Post,
                "PUT" => HttpMethod.Put,
                "DELETE" => HttpMethod.Delete,
                _ => HttpMethod.Get
            };

            using var request = new HttpRequestMessage(method, uri);

            if (method != HttpMethod.Get && input.Parameters.TryGetValue("body", out var body))
            {
                request.Content = new StringContent(
                    body?.ToString() ?? "",
                    System.Text.Encoding.UTF8,
                    "application/json");
            }

            var response = await client.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            return ToolResult.Ok(new
            {
                statusCode = (int)response.StatusCode,
                body = content.Length > 5000 ? content[..5000] + "...[truncated]" : content,
                headers = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value))
            });
        }
        catch (TaskCanceledException)
        {
            return ToolResult.Fail("Timeout na requisição HTTP.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP tool error for {Url}", url);
            return ToolResult.Fail($"Erro HTTP: {ex.Message}");
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);
}

public class CalculatorTool : ITool
{
    public string Id => "calculator";
    public string Name => "Calculator Tool";
    public string Description => "Realiza cálculos matemáticos básicos.";
    public ToolCategory Category => ToolCategory.Tasks;
    public bool RequiresAuth => false;

    public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!input.Parameters.TryGetValue("expression", out var exprObj) || exprObj is not string expression)
            return Task.FromResult(ToolResult.Fail("Parâmetro 'expression' obrigatório."));

        try
        {
            var result = EvaluateSimple(expression);
            return Task.FromResult(ToolResult.Ok(new { expression, result }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(ToolResult.Fail($"Erro no cálculo: {ex.Message}"));
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    private static double EvaluateSimple(string expression)
    {
        // Parser seguro para operações básicas (sem eval dinâmico)
        var tokens = Tokenize(expression.Trim());
        return ParseExpression(tokens, 0, out _);
    }

    private static List<string> Tokenize(string expr)
    {
        var tokens = new List<string>();
        var current = "";
        foreach (var c in expr)
        {
            if (char.IsDigit(c) || c == '.')
            {
                current += c;
            }
            else if ("+-*/".Contains(c))
            {
                if (!string.IsNullOrEmpty(current)) tokens.Add(current);
                tokens.Add(c.ToString());
                current = "";
            }
            else if (c == ' ') 
            {
                if (!string.IsNullOrEmpty(current)) tokens.Add(current);
                current = "";
            }
        }
        if (!string.IsNullOrEmpty(current)) tokens.Add(current);
        return tokens;
    }

    private static double ParseExpression(List<string> tokens, int start, out int end)
    {
        if (tokens.Count == 0) { end = 0; return 0; }

        var result = double.Parse(tokens[start]);
        var i = start + 1;

        while (i < tokens.Count - 1)
        {
            var op = tokens[i];
            var right = double.Parse(tokens[i + 1]);
            result = op switch
            {
                "+" => result + right,
                "-" => result - right,
                "*" => result * right,
                "/" => right != 0 ? result / right : throw new DivideByZeroException(),
                _ => result
            };
            i += 2;
        }

        end = i;
        return result;
    }
}

public class FileSearchTool : ITool
{
    public string Id => "file-search";
    public string Name => "File Search Tool";
    public string Description => "Busca arquivos e conteúdo por padrão no sistema de arquivos local.";
    public ToolCategory Category => ToolCategory.Storage;
    public bool RequiresAuth => false;

    public Task<ToolResult> ExecuteAsync(ToolInput input, CancellationToken ct = default)
    {
        if (!input.Parameters.TryGetValue("path", out var pathObj) || pathObj is not string basePath)
            return Task.FromResult(ToolResult.Fail("Parâmetro 'path' obrigatório."));

        var pattern = input.Parameters.TryGetValue("pattern", out var p) ? p?.ToString() ?? "*.*" : "*.*";

        if (!Directory.Exists(basePath))
            return Task.FromResult(ToolResult.Fail($"Diretório não encontrado: {basePath}"));

        return input.Action.ToLowerInvariant() switch
        {
            "list" => ListFiles(basePath, pattern),
            "search" => SearchContent(basePath, pattern, input),
            _ => ListFiles(basePath, pattern)
        };
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default) => Task.FromResult(true);

    private static Task<ToolResult> ListFiles(string basePath, string pattern)
    {
        var files = Directory.GetFiles(basePath, pattern, SearchOption.TopDirectoryOnly)
            .Take(50)
            .Select(f => new { name = Path.GetFileName(f), path = f, size = new FileInfo(f).Length })
            .ToList();

        return Task.FromResult(ToolResult.Ok(new { count = files.Count, files }));
    }

    private static Task<ToolResult> SearchContent(string basePath, string pattern, ToolInput input)
    {
        var query = input.Parameters.TryGetValue("query", out var q) ? q?.ToString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult(ToolResult.Fail("Parâmetro 'query' obrigatório para search."));

        var matches = new List<object>();
        foreach (var file in Directory.GetFiles(basePath, pattern, SearchOption.AllDirectories).Take(100))
        {
            try
            {
                var content = File.ReadAllText(file);
                if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new { file = Path.GetFileName(file), path = file });
                }
            }
            catch { /* skip unreadable files */ }
        }

        return Task.FromResult(ToolResult.Ok(new { query, count = matches.Count, matches }));
    }
}
