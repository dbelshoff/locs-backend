using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Polly;
using projetoX.Config;
using projetoX.Data;
using projetoX.DTOs;
using projetoX.Models;

namespace projetoX.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EmpresaController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _googleApiKey;
    private readonly IMemoryCache _cache;
    private readonly AppDbContext _dbContext;
    private readonly Dictionary<string, string> _dicionarioTipos;
    private readonly string _fotosPath;
    private readonly ILogger<EmpresaController> _logger;
    private readonly EmpresaControllerSettings _settings;

    public EmpresaController(
        ILogger<EmpresaController> logger,
        IHttpClientFactory httpClientFactory,
        string googleApiKey,
        IMemoryCache cache,
        AppDbContext dbContext,
        IOptions<EmpresaControllerSettings> settings
    )
    {
        _httpClientFactory = httpClientFactory;
        _googleApiKey =
            Environment.GetEnvironmentVariable("API_GOOGLE")
            ?? throw new InvalidOperationException("API_GOOGLE environment variable not set.");
        _cache = cache;
        _dbContext = dbContext;
        _logger = logger;
        _settings = settings.Value;

        _fotosPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos");

        if (!Directory.Exists(_fotosPath))
        {
            Directory.CreateDirectory(_fotosPath);
        }

        var dicionarioPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "wwwroot",
            "assets",
            "dicionario.json"
        );
        if (System.IO.File.Exists(dicionarioPath))
        {
            var json = System.IO.File.ReadAllText(dicionarioPath);
            var tempDict =
                JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                ?? new Dictionary<string, string>();
            _dicionarioTipos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in tempDict)
            {
                _dicionarioTipos[kvp.Key.ToLower()] = kvp.Value;
            }
        }
        else
        {
            _dicionarioTipos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _logger.LogWarning("Arquivo dicionario.json não encontrado.");
        }
    }

    [HttpGet("filtrar-empresas")]
    [AllowAnonymous]

public async Task<IActionResult> FiltrarEmpresas(
    [FromQuery(Name = "lat")] double latitude,
    [FromQuery(Name = "lon")] double longitude,
    [FromQuery] string estado,
    [FromQuery] string? cidade = null,
    [FromQuery] string? bairro = null,
    [FromQuery] string? query = null,
    [FromQuery] bool somenteBanco = false
)


    /*public async Task<IActionResult> FiltrarEmpresas(
        [FromQuery(Name = "lat")] double latitude,
        [FromQuery(Name = "lon")] double longitude,
        [FromQuery] string estado,
        [FromQuery] string? cidade = null,
        [FromQuery] string? bairro = null,
        [FromQuery] string? query = null
    )*/
    {
        try
        {
            if (latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180)
                return BadRequest("Coordenadas fora do intervalo válido: latitude deve estar entre -90 e 90, longitude entre -180 e 180.");
            if (string.IsNullOrEmpty(estado))
                return BadRequest("Estado é obrigatório.");

            estado = estado.Trim();
            cidade = cidade?.Trim();
            bairro = bairro?.Trim();
            query = query?.Trim();

            var cacheKey = $"filtrar_{latitude}_{longitude}_{estado}_{cidade}_{bairro}_{query}_{somenteBanco}";
            //var cacheKey = $"filtrar_{latitude}_{longitude}_{estado}_{cidade}_{bairro}_{query}";
            if (_cache.TryGetValue(cacheKey, out List<EstabelecimentoDTO>? cachedResult) && cachedResult != null)
            {
                _logger.LogInformation("Dados retornados do cache para a chave: {CacheKey}", cacheKey);
                return Ok(cachedResult);
            }

            _logger.LogInformation("Consultando estabelecimentos no banco de dados para estado: {Estado}, cidade: {Cidade}, bairro: {Bairro}, query: {Query}", estado, cidade, bairro, query);
            var queryBanco = _dbContext.Estabelecimentos.Where(e => e.Estado == estado);

            if (!string.IsNullOrEmpty(cidade))
                queryBanco = queryBanco.Where(e => e.Cidade == cidade);
            if (!string.IsNullOrEmpty(bairro))
                queryBanco = queryBanco.Where(e => e.Bairro == bairro);

            string filterQuery = query ?? string.Empty;

            if (!string.IsNullOrEmpty(query))
                queryBanco = queryBanco.Where(e => EF.Functions.Like(e.Types, $"%{filterQuery}%"));



            var estabelecimentosBanco = await queryBanco.ToListAsync();
            _logger.LogInformation("Retornados {Count} estabelecimentos do banco de dados com filtro padrão", estabelecimentosBanco.Count);

            var resultadoFinal = estabelecimentosBanco.Select(MapearParaDTO).ToList();

            if (somenteBanco)
{
    _logger.LogInformation(
        "Retornando somente resultados do banco para carregamento rápido. Quantidade: {Count}",
        resultadoFinal.Count
    );

    _cache.Set(cacheKey, resultadoFinal, TimeSpan.FromSeconds(30));

    return Ok(resultadoFinal);
}

            bool dbSufficient = resultadoFinal.Count >= _settings.MinResultsFromDatabase &&
                                estabelecimentosBanco.All(e => e.UltimaAtualizacao > DateTime.UtcNow.AddDays(-30));

            if (!dbSufficient && !string.IsNullOrEmpty(query))
            {
                string typeCacheKey = $"translated_type_for_{query.ToLowerInvariant()}";
                string? translatedType = null;

                if (!_cache.TryGetValue(typeCacheKey, out translatedType))
                {
                    _logger.LogInformation("Buscando tipo principal da API Google para query: {Query}", query);

                    var client = CreateHttpClient();
                    const float raio = 500.0f;
                    var retryPolicy = CreateRetryPolicy("Places API - Type Inference");

                    string endpoint = "https://places.googleapis.com/v1/places";
                    string fieldMask = "places.primaryType";

                    var response = await retryPolicy.ExecuteAsync(async () =>
                    {
                        var requestBody = new
                        {
                            textQuery = query,
                            locationBias = new
                            {
                                circle = new
                                {
                                    center = new { latitude = latitude, longitude = longitude },
                                    radius = raio,
                                },
                            },
                            languageCode = "pt-BR",
                            regionCode = "BR",
                            maxResultCount = 1,
                        };

                        var jsonContent = new StringContent(
                            JsonSerializer.Serialize(requestBody),
                            System.Text.Encoding.UTF8,
                            "application/json"
                        );

                        var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}:searchText")
                        {
                            Content = jsonContent,
                        };
                        request.Headers.Add("X-Goog-FieldMask", fieldMask);
                        request.Headers.Add("Accept-Encoding", "gzip");
                        request.Headers.Add("User-Agent", "projetoX (gzip)");
                        request.Headers.Add("X-Goog-Api-Key", _googleApiKey);

                        return await client.SendAsync(request);
                    });

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var data = JObject.Parse(json);
                        var places = data["places"] as JArray;

                        if (places != null && places.Count > 0)
                        {
                            var primaryTypeEng = places[0]["primaryType"]?.ToString();
                            if (!string.IsNullOrEmpty(primaryTypeEng))
                            {
                                translatedType = _dicionarioTipos.TryGetValue(primaryTypeEng.ToLower(), out var traducao)
                                    ? traducao
                                    : null;
                                if (!string.IsNullOrEmpty(translatedType))
                                {
                                    _cache.Set(typeCacheKey, translatedType, TimeSpan.FromDays(7));
                                    _logger.LogInformation("Tipo principal traduzido para '{TranslatedType}' e armazenado no cache para query: {Query}", translatedType, query);
                                }
                            }
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Erro ao consultar Google Places API para tipo: {ErrorContent}", errorContent);
                    }
                }

                if (!string.IsNullOrEmpty(translatedType) && translatedType != query)
                {
                    _logger.LogInformation("Consultando banco novamente com tipo traduzido: {TranslatedType}", translatedType);

                    queryBanco = _dbContext.Estabelecimentos.Where(e => e.Estado == estado);

                    if (!string.IsNullOrEmpty(cidade))
                        queryBanco = queryBanco.Where(e => e.Cidade == cidade);
                    if (!string.IsNullOrEmpty(bairro))
                        queryBanco = queryBanco.Where(e => e.Bairro == bairro);

                    queryBanco = queryBanco.Where(e => EF.Functions.Like(e.Types, $"%{translatedType}%"));

                    estabelecimentosBanco = await queryBanco.ToListAsync();
                    _logger.LogInformation("Retornados {Count} estabelecimentos do banco de dados com filtro traduzido", estabelecimentosBanco.Count);

                    resultadoFinal = estabelecimentosBanco.Select(MapearParaDTO).ToList();

                    dbSufficient = resultadoFinal.Count >= _settings.MinResultsFromDatabase &&
                                   estabelecimentosBanco.All(e => e.UltimaAtualizacao > DateTime.UtcNow.AddDays(-30));
                }
            }

            if (dbSufficient)
            {
                _logger.LogInformation("Resultados do banco são suficientes e recentes. Armazenando no cache: {CacheKey}", cacheKey);
                _cache.Set(cacheKey, resultadoFinal, TimeSpan.FromHours(1));
                return Ok(resultadoFinal);
            }

            _logger.LogInformation("Consultando Google Places API para latitude: {Latitude}, longitude: {Longitude}, query: {Query}", latitude, longitude, query);
            var clientFull = CreateHttpClient();
            const float radius = 500.0f;
            var retryPolicyFull = CreateRetryPolicy("Places API");
            string endpointFull = "https://places.googleapis.com/v1/places";
            string fieldMaskFull =
                "places.id,places.displayName,places.formattedAddress,places.location,places.types,places.rating,places.nationalPhoneNumber,places.currentOpeningHours,places.addressComponents,places.photos";

            var responseFull = await retryPolicyFull.ExecuteAsync(async () =>
            {
                HttpRequestMessage requestFull;
                if (!string.IsNullOrEmpty(query))
                {
                    var requestBody = new
                    {
                        textQuery = query,
                        locationBias = new
                        {
                            circle = new
                            {
                                center = new { latitude = latitude, longitude = longitude },
                                radius = radius,
                            },
                        },
                        languageCode = "pt-BR",
                        regionCode = "BR",
                        maxResultCount = _settings.MaxResultsSearchText,
                    };
                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    requestFull = new HttpRequestMessage(HttpMethod.Post, $"{endpointFull}:searchText")
                    {
                        Content = jsonContent,
                    };
                    requestFull.Headers.Add("X-Goog-FieldMask", fieldMaskFull);
                }
                else
                {
                    var requestBody = new
                    {
                        locationRestriction = new
                        {
                            circle = new
                            {
                                center = new { latitude = latitude, longitude = longitude },
                                radius = radius,
                            },
                        },
                        languageCode = "pt-BR",
                        regionCode = "BR",
                        maxResultCount = _settings.MaxResultsSearchNearby,
                    };
                    var jsonContent = new StringContent(
                        JsonSerializer.Serialize(requestBody),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );

                    requestFull = new HttpRequestMessage(HttpMethod.Post, $"{endpointFull}:searchNearby")
                    {
                        Content = jsonContent,
                    };
                    requestFull.Headers.Add("X-Goog-FieldMask", fieldMaskFull);
                }

                requestFull.Headers.Add("Accept-Encoding", "gzip");
                requestFull.Headers.Add("User-Agent", "projetoX (gzip)");
                requestFull.Headers.Add("X-Goog-Api-Key", _googleApiKey);

                return await clientFull.SendAsync(requestFull);
            });

            if (!responseFull.IsSuccessStatusCode)
            {
                var errorContent = await responseFull.Content.ReadAsStringAsync();
                _logger.LogError("Places API Error: {errorContent}", errorContent);
                return StatusCode((int)responseFull.StatusCode, $"Erro ao consultar Google Places API: {errorContent}");
            }

            var jsonFull = await responseFull.Content.ReadAsStringAsync();
            var dataFull = JObject.Parse(jsonFull);

            var resultadosApi = dataFull["places"]?.ToObject<List<JObject>>() ?? new List<JObject>();
            _logger.LogInformation("Google Places API retornou {Count} resultados", resultadosApi.Count);

            if (!resultadosApi.Any())
            {
                _logger.LogInformation("API retornou zero resultados. Armazenando resultados do banco no cache: {CacheKey}", cacheKey);
                _cache.Set(cacheKey, resultadoFinal, TimeSpan.FromHours(1));
                return Ok(resultadoFinal);
            }

            var estabelecimentosApi = await ProcessarResultados(clientFull, resultadosApi);
            _logger.LogInformation("Processados {Count} estabelecimentos da API", estabelecimentosApi.Count);

            resultadoFinal.AddRange(estabelecimentosApi.Where(e => !resultadoFinal.Any(r => r.PlaceId == e.PlaceId)));

            if (resultadoFinal.Any())
            {
                _logger.LogInformation("Armazenando {Count} resultados combinados no cache: {CacheKey}", resultadoFinal.Count, cacheKey);
                _cache.Set(cacheKey, resultadoFinal, TimeSpan.FromHours(1));
            }

            return Ok(resultadoFinal);
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao filtrar empresas: {Message}", ex.Message);
            return StatusCode(500, $"Erro ao filtrar empresas: {ex.Message}");
        }
    }

    [HttpGet("foto")]
    [AllowAnonymous]
    public IActionResult ObterFoto([FromQuery] string fotoNome)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fotoNome))
                return BadRequest("Nome da foto é obrigatório.");

            var fotoPath = Path.Combine(_fotosPath, fotoNome);
            if (!System.IO.File.Exists(fotoPath))
                return NotFound("Foto não encontrada.");

            var imageBytes = System.IO.File.ReadAllBytes(fotoPath);
            return File(imageBytes, "image/jpeg");
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro ao obter foto: {Message}", ex.Message);
            return StatusCode(500, "Erro ao obter foto");
        }
    }

    private async Task<List<EstabelecimentoDTO>> ProcessarResultados(
        HttpClient client,
        List<JObject> resultadosApi
    )
    {
        var estabelecimentos = new List<EstabelecimentoDTO>();
        var retryPolicy = CreateRetryPolicy("Place Photos");

        foreach (var place in resultadosApi)
        {
            var placeId = place["id"]?.ToString();
            if (string.IsNullOrEmpty(placeId))
            {
                _logger.LogWarning("PlaceId nulo ou vazio, ignorando...");
                continue;
            }

            if (!Regex.IsMatch(placeId, @"^[A-Za-z0-9_-]+$"))
            {
                _logger.LogWarning(
                    "PlaceId {PlaceId} contém caracteres inválidos, ignorando...",
                    placeId
                );
                continue;
            }

            var existente = await _dbContext.Estabelecimentos.FirstOrDefaultAsync(e =>
                e.PlaceId == placeId
            );
            if (existente != null && existente.UltimaAtualizacao > DateTime.UtcNow.AddDays(-30))
            {
                estabelecimentos.Add(MapearParaDTO(existente));
                continue;
            }

            var entidade = existente ?? new Estabelecimento();
            var addressComponents = place["addressComponents"]?.ToObject<JArray>();

            string? logradouro = null,
                   numero = null,
                   bairro = null,
                   cidade = null,
                   estado = null,
                   cep = null;

            if (addressComponents != null)
            {
                foreach (var component in addressComponents)
                {
                    var componentTypes = component["types"]?.ToObject<List<string>>();
                    if (componentTypes == null) continue;

                    if (componentTypes.Contains("street_number"))
                        numero = component["longText"]?.ToString();
                    if (componentTypes.Contains("route"))
                        logradouro = component["longText"]?.ToString();
                    if (componentTypes.Contains("sublocality") || componentTypes.Contains("neighborhood"))
                        bairro = component["longText"]?.ToString();
                    if (componentTypes.Contains("locality") || componentTypes.Contains("administrative_area_level_2"))
                        cidade = component["longText"]?.ToString();
                    if (componentTypes.Contains("administrative_area_level_1"))
                        estado = component["shortText"]?.ToString();
                    /*if (componentTypes.Contains("postal_code"))
                        cep = component["long_name"]?.ToString();*/
                   if (componentTypes.Contains("postal_code"))
                    cep = component["longText"]?.ToString();


                }
            }

            // Construir o FormattedAddress
            var formattedAddressParts = new List<string>
            {
                logradouro ?? "",
                numero ?? "",
                bairro != null ? $"- {bairro}" : "",
                cidade ?? "",
                estado ?? "",
                cep ?? "",
            };
            entidade.FormattedAddress = string.Join(", ", formattedAddressParts.Where(p => !string.IsNullOrEmpty(p)));

            string? fotoNome = null;
            var photos = place?["photos"]?.ToObject<List<JObject>>();
            if (photos?.Any() == true)
            {
                var photoReference = photos.First()["name"]?.ToString();
                if (!string.IsNullOrEmpty(photoReference))
                {
                    var photoUrl =
                        $"https://places.googleapis.com/v1/{photoReference}/media?maxHeightPx=400&maxWidthPx=300&key={_googleApiKey}";
                    var photoResponse = await retryPolicy.ExecuteAsync(async () =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Get, photoUrl);
                        request.Headers.Add("Accept-Encoding", "gzip");
                        request.Headers.Add("User-Agent", "projetoX (gzip)");

                        var result = await client.SendAsync(request);
                        return result;
                    });

                    if (photoResponse.IsSuccessStatusCode)
                    {
                        var photoBytes = await photoResponse.Content.ReadAsByteArrayAsync();
                        fotoNome = $"{placeId}_{Guid.NewGuid().ToString("N")}.jpg";
                        var fotoPath = Path.Combine(_fotosPath, fotoNome);
                        await System.IO.File.WriteAllBytesAsync(fotoPath, photoBytes);
                    }
                    else
                    {
                        _logger.LogError(
                            "Erro ao baixar foto para {PlaceId}: {Error}",
                            placeId,
                            await photoResponse.Content.ReadAsStringAsync()
                        );
                    }
                }
            }
            else
            {
                _logger.LogInformation("Nenhuma foto disponível para PlaceId {PlaceId}.", placeId);
            }

            var placeLat = place["location"]?["latitude"]?.ToObject<double?>();
            var placeLng = place["location"]?["longitude"]?.ToObject<double?>();
            var placeRating = place["rating"]?.ToObject<double?>();
            var telefone = place["nationalPhoneNumber"]?.ToString();

            entidade.PlaceId = placeId;
            entidade.Nome = place["displayName"]?["text"]?.ToString() ?? "Sem nome";
            entidade.Logradouro = logradouro;
            entidade.Numero = numero;
            entidade.Bairro = bairro;
            entidade.Cidade = cidade;
            entidade.Estado = estado;
            entidade.Cep = cep; // Atribuir o CEP extraído
            entidade.Latitude = placeLat ?? 0.0;
            entidade.Longitude = placeLng ?? 0.0;
            entidade.Telefone = telefone;
            entidade.AbertoAgora = place["currentOpeningHours"]?["openNow"]?.ToObject<bool?>();
            entidade.Foto = fotoNome;

            var tipos =
                place
                    ?["types"]?.ToObject<List<string>>()
                    ?.Where(t =>
                        !new[]
                        {
                            "point_of_interest",
                            "establishment",
                            "locality",
                            "political",
                        }.Contains(t)
                    )
                    ?.Select(t =>
                    {
                        return _dicionarioTipos.TryGetValue(t.ToLower(), out var traducao)
                            ? traducao
                            : t;
                    })
                    ?.ToList() ?? new List<string>();

            entidade.Types = string.Join(",", tipos);
            entidade.Rating = placeRating ?? 0.0;
            entidade.UltimaAtualizacao = DateTime.UtcNow;

            if (existente == null)
            {
                _dbContext.Estabelecimentos.Add(entidade);
            }
            await _dbContext.SaveChangesAsync();

            estabelecimentos.Add(MapearParaDTO(entidade));
        }

        return estabelecimentos;
    }

    private EstabelecimentoDTO MapearParaDTO(Estabelecimento entidade)
    {
        return new EstabelecimentoDTO
        {
            PlaceId = entidade.PlaceId ?? string.Empty,
            Nome = entidade.Nome ?? "Sem nome",
            FormattedAddress = entidade.FormattedAddress ?? string.Empty,
            Logradouro = entidade.Logradouro,
            Numero = entidade.Numero,
            Bairro = entidade.Bairro,
            Cidade = entidade.Cidade,
            Estado = entidade.Estado,
            Cep = entidade.Cep, // Incluir o campo Cep
            Latitude = entidade.Latitude,
            Longitude = entidade.Longitude,
            Telefone = entidade.Telefone,
            AbertoAgora = entidade.AbertoAgora,
            FotoUrl = string.IsNullOrEmpty(entidade.Foto) ? null : entidade.Foto,
            Types = string.IsNullOrEmpty(entidade.Types)
                ? new List<string>()
                : entidade
                    .Types.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .ToList(),
            TypesString = entidade.Types ?? string.Empty,
            Rating = entidade.Rating,
        };
    }

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy(string apiType)
    {
        return Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        "Tentativa {RetryCount} falhou ({ApiType}). Aguardando {TotalSeconds}s antes de retries.",
                        retryCount,
                        apiType,
                        timeSpan.TotalSeconds
                    );
                }
            );

    }
}
