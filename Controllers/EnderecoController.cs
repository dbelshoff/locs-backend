using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
//using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using projetoX.Data;
using projetoX.DTOs;

namespace projetoX.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EnderecoController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _googleApiKey;

    public EnderecoController(IHttpClientFactory httpClientFactory, string googleApiKey)
    {
        _httpClientFactory = httpClientFactory;
        _googleApiKey =
            googleApiKey
            ?? throw new ArgumentNullException(
                nameof(googleApiKey),
                "GoogleApiKey não pode ser nulo."
            );
    }

    [HttpGet("geolocalizacao")]
    [AllowAnonymous]
    public async Task<IActionResult> ObterEnderecoPorGeolocalizacao(
        [FromQuery] double lat,
        [FromQuery] double lon
    )
    {
        try
        {
            // Validar coordenadas
            if (lat == 0 || lon == 0)
                return BadRequest(
                    "Coordenadas inválidas: latitude e longitude não podem ser zero."
                );
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
                return BadRequest(
                    "Coordenadas fora do intervalo válido: latitude deve estar entre -90 e 90, longitude entre -180 e 180."
                );

            var client = _httpClientFactory.CreateClient();
            // Formatar coordenadas com CultureInfo.InvariantCulture
            var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var latlng = Uri.EscapeDataString($"{latStr},{lonStr}");
            var url =
                $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latlng}&key={_googleApiKey}&language=pt-BR";
            //Console.WriteLine($"Geocoding Request URL: {url}");
            //Console.WriteLine($"Raw latlng parameter: {latStr},{lonStr}");

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Geocoding API Error: {errorContent}");
                return BadRequest($"Erro ao consultar Google Geocoding API: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            //Console.WriteLine($"Geocoding API Response: {content}");
            var json = JObject.Parse(content);
            var status = json["status"]?.ToString();

            if (status != "OK")
            {
                //Console.WriteLine($"Geocoding API Status: {status}");
                return BadRequest(
                    $"Google Geocoding API retornou status: {status}. Detalhes: {json["error_message"]?.ToString() ?? "Sem detalhes."}"
                );
            }

            var results = json["results"]?.ToObject<List<JObject>>();
            if (results == null || results.Count == 0)
            {
                //Console.WriteLine("Nenhum resultado encontrado pela Geocoding API.");
                return NotFound("Endereço não encontrado para essas coordenadas.");
            }

            var address = results[0]["address_components"]?.ToObject<List<JObject>>();
            if (address == null)
            {
                //Console.WriteLine("Componentes de endereço não encontrados na resposta.");
                return NotFound("Componentes de endereço não encontrados.");
            }

            string estado = "",
                cidade = "",
                bairro = "";
            foreach (var component in address)
            {
                var types = component["types"]?.ToObject<List<string>>();
                if (types == null)
                    continue;

                if (types.Contains("administrative_area_level_1"))
                    estado = component["short_name"]?.ToString() ?? "";
                if (types.Contains("locality") || types.Contains("administrative_area_level_2"))
                    cidade = component["long_name"]?.ToString() ?? cidade;
                if (
                    types.Contains("sublocality")
                    || types.Contains("neighborhood")
                    || types.Contains("sublocality_level_1")
                )
                    bairro = component["long_name"]?.ToString() ?? bairro;
            }

            // Logar componentes de endereço para depuração
            //Console.WriteLine(
                //$"Address Components: {JsonConvert.SerializeObject(address, Formatting.Indented)}"
           // );

            return Ok(
                new
                {
                    estado,
                    cidade,
                    bairro,
                }
            );
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Geocoding Exception: {ex.Message}");
            return StatusCode(500, $"Erro interno: {ex.Message}");
        }
    }

    [HttpGet("coordenadas")]
    [AllowAnonymous]
    public async Task<IActionResult> ObterCoordenadasPorEndereco(
        [FromQuery] string estado,
        [FromQuery] string? cidade = null,
        [FromQuery] string? bairro = null
    )
    {
        try
        {
            // Validar parâmetros
            if (string.IsNullOrWhiteSpace(estado))
                return BadRequest("Estado é obrigatório e não pode ser vazio.");
            if (estado.Length != 2 || !estado.All(char.IsLetter))
                return BadRequest("Estado deve ser uma sigla de 2 letras (ex.: 'ES').");

            // Construir string de endereço dinamicamente
            var enderecoPartes = new List<string>();
            if (!string.IsNullOrWhiteSpace(bairro))
                enderecoPartes.Add(bairro);
            if (!string.IsNullOrWhiteSpace(cidade))
                enderecoPartes.Add(cidade);
            enderecoPartes.Add(estado);
            enderecoPartes.Add("Brasil");

            var endereco = string.Join(", ", enderecoPartes);
            var enderecoEncoded = Uri.EscapeDataString(endereco);
            var url =
                $"https://maps.googleapis.com/maps/api/geocode/json?address={enderecoEncoded}&key={_googleApiKey}&language=pt-BR";
            //Console.WriteLine($"Geocoding Request URL: {url}");
            //Console.WriteLine($"Raw address parameter: {endereco}");

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"Geocoding API Error: {errorContent}");
                return BadRequest($"Erro ao consultar Google Geocoding API: {errorContent}");
            }

            var content = await response.Content.ReadAsStringAsync();
            //Console.WriteLine($"Geocoding API Response: {content}");
            var json = JObject.Parse(content);
            var status = json["status"]?.ToString();

            if (status != "OK")
            {
                //Console.WriteLine($"Geocoding API Status: {status}");
                return BadRequest(
                    $"Google Geocoding API retornou status: {status}. Detalhes: {json["error_message"]?.ToString() ?? "Sem detalhes."}"
                );
            }

            var results = json["results"]?.ToObject<List<JObject>>();
            if (results == null || results.Count == 0)
            {
                //Console.WriteLine("Nenhum resultado encontrado pela Geocoding API.");
                return NotFound("Coordenadas não encontradas para o endereço fornecido.");
            }

            var location = results[0]["geometry"]?["location"]?.ToObject<JObject>();
            if (location == null)
            {
                //Console.WriteLine("Coordenadas não encontradas na resposta.");
                return NotFound("Coordenadas não encontradas na resposta.");
            }

            var latitude = location["lat"]?.ToObject<double>();
            var longitude = location["lng"]?.ToObject<double>();
            if (latitude == null || longitude == null)
            {
                //Console.WriteLine("Valores de latitude ou longitude inválidos na resposta.");
                return NotFound("Valores de latitude ou longitude inválidos na resposta.");
            }

            // Logar coordenadas para depuração
            //Console.WriteLine(
                //$"Coordenadas encontradas: Latitude={latitude}, Longitude={longitude}"
           // );

            return Ok(new { latitude, longitude });
        }
        catch (Exception ex)
        {
            //Console.WriteLine($"Geocoding Exception: {ex.Message}");
            return StatusCode(500, $"Erro interno: {ex.Message}");
        }
    }

    private string ObterSiglaEstado(string nomeEstado)
    {
        var estados = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Acre", "AC" },
            { "Alagoas", "AL" },
            { "Amapá", "AP" },
            { "Amazonas", "AM" },
            { "Bahia", "BA" },
            { "Ceará", "CE" },
            { "Distrito Federal", "DF" },
            { "Espírito Santo", "ES" },
            { "Goiás", "GO" },
            { "Maranhão", "MA" },
            { "Mato Grosso", "MT" },
            { "Mato Grosso do Sul", "MS" },
            { "Minas Gerais", "MG" },
            { "Pará", "PA" },
            { "Paraíba", "PB" },
            { "Paraná", "PR" },
            { "Pernambuco", "PE" },
            { "Piauí", "PI" },
            { "Rio de Janeiro", "RJ" },
            { "Rio Grande do Norte", "RN" },
            { "Rio Grande do Sul", "RS" },
            { "Rondônia", "RO" },
            { "Roraima", "RR" },
            { "Santa Catarina", "SC" },
            { "São Paulo", "SP" },
            { "Sergipe", "SE" },
            { "Tocantins", "TO" },
        };

        return estados.TryGetValue(nomeEstado ?? "", out var sigla) ? sigla : "";
    }
}
