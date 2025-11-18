using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProyectoParalelismoReal.Models;

namespace ProyectoParalelismoReal.Services
{
    /// <summary>
    /// Scraper de juegos con NIVEL 1 de paralelismo mejorado
    /// Obtiene múltiples páginas de Steam simultáneamente
    /// </summary>
    public static class ScraperJuegos
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly string _ua = "ProyectoScraperVideojuegos/1.0 (contacto: equipo@example.com)";

        // ⚡ Control de concurrencia: máximo 4 páginas descargándose simultáneamente
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(4, 4);

        static ScraperJuegos()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(_ua);
            _http.Timeout = TimeSpan.FromSeconds(25);
        }

        /// <summary>
        /// NIVEL 1 MEJORADO: Obtiene N juegos de Steam con paralelismo real
        /// Descarga múltiples páginas simultáneamente usando Task.WhenAll
        /// </summary>
        /// <param name="n">Número total de juegos a obtener</param>
        /// <param name="perPage">Juegos por página</param>
        /// <param name="delayMs">Delay entre requests (anti-throttling)</param>
        /// <returns>Lista de juegos obtenidos</returns>
        public static async Task<List<Juego>> ObtenerTopNSteamAsync(int n = 200, int perPage = 50, int delayMs = 600)
        {
            Console.WriteLine($"[Nivel 1] Iniciando descarga paralela de {n} juegos...");

            // Calcular cuántas páginas necesitamos
            int pages = (int)Math.Ceiling((double)n / perPage);
            Console.WriteLine($"[Nivel 1] Se descargarán {pages} páginas de {perPage} juegos cada una");

            // Generar lista de offsets para cada página
            var pageStarts = new List<int>();
            for (int i = 0; i < pages; i++)
            {
                pageStarts.Add(i * perPage); // [0, 50, 100, 150, ...]
            }

            // ⚡ PARALELISMO REAL: Crear tareas para descargar TODAS las páginas simultáneamente
            var tareasPaginas = pageStarts.Select(async (start, index) =>
            {
                await _semaphore.WaitAsync(); // ⚡ Esperar turno (máx 4 simultáneas)
                try
                {
                    // Delay escalonado para evitar saturar el servidor
                    await Task.Delay(index * delayMs / 4);

                    Console.WriteLine($"[Nivel 1] Descargando página {index + 1}/{pages} (offset {start})...");

                    var html = await FetchSteamSearchFragmentAsync(start, perPage);
                    var parsed = ParseSteamSearchFragment(html);

                    Console.WriteLine($"[Nivel 1] ✓ Página {index + 1}/{pages} completada ({parsed.Count} juegos)");

                    // Delay anti-throttling
                    await Task.Delay(delayMs);

                    return parsed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR Nivel 1] Página offset={start}: {ex.Message}");
                    return new List<Juego>(); // Retornar lista vacía en caso de error
                }
                finally
                {
                    _semaphore.Release(); // ⚡ Liberar turno
                }
            }).ToList();

            // ⚡ Task.WhenAll: Esperar a que TODAS las páginas se descarguen
            Console.WriteLine($"[Nivel 1] Esperando descarga paralela de {pages} páginas...");
            var resultadosPorPagina = await Task.WhenAll(tareasPaginas);

            // Combinar todos los resultados en una sola lista
            var todosLosJuegos = resultadosPorPagina
                .SelectMany(lista => lista) // Aplanar las listas
                .ToList();

            Console.WriteLine($"[Nivel 1] ✓ Total descargado: {todosLosJuegos.Count} juegos");

            // Limitar a N juegos si obtuvimos más de los solicitados
            if (todosLosJuegos.Count > n)
            {
                todosLosJuegos = todosLosJuegos.Take(n).ToList();
                Console.WriteLine($"[Nivel 1] Limitado a {n} juegos solicitados");
            }

            return todosLosJuegos;
        }

        /// <summary>
        /// Descarga una página específica de resultados de Steam
        /// </summary>
        private static async Task<string> FetchSteamSearchFragmentAsync(int start, int count)
        {
            var uri = $"https://store.steampowered.com/search/results/?query=&start={start}&count={count}&filter=topsellers&cc=US&l=english";

            using var resp = await _http.GetAsync(uri);
            resp.EnsureSuccessStatusCode();
            var text = await resp.Content.ReadAsStringAsync();

            // Steam puede devolver JSON con HTML embebido
            if (text.Contains("\"results_html\""))
            {
                var jo = JObject.Parse(text);
                var html = (string?)jo["results_html"] ?? string.Empty;
                return html;
            }

            return text;
        }

        /// <summary>
        /// Parsea el HTML de una página de Steam y extrae los juegos
        /// </summary>
        private static List<Juego> ParseSteamSearchFragment(string html)
        {
            var list = new List<Juego>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            string Clean(string? s) => (s ?? string.Empty)
                .Replace("\r", "").Replace("\n", "").Replace("\t", "")
                .Replace(" ", " ").Trim();

            var nodes = doc.DocumentNode.SelectNodes("//a[contains(concat(' ', normalize-space(@class), ' '), ' search_result_row ')]");
            if (nodes == null) return list;

            foreach (var node in nodes)
            {
                try
                {
                    var titleNode = node.SelectSingleNode(".//span[@class='title']");
                    var title = Clean(titleNode?.InnerText);
                    var href = node.GetAttributeValue("href", string.Empty);

                    var priceNode = node.SelectSingleNode(".//div[contains(@class,'search_price')]");
                    var priceText = Clean(priceNode?.InnerText);

                    var discNode = node.SelectSingleNode(".//div[contains(@class,'search_discount')]/span");
                    var discount = Clean(discNode?.InnerText);

                    var j = new Juego
                    {
                        Nombre = title,
                        FuenteInicialUrl = href ?? string.Empty
                    };

                    var sres = new StoreResult
                    {
                        StoreName = "steam_fragment",
                        Url = href,
                        PriceRaw = priceText,
                        ExtraInfo = discount
                    };
                    j.Tiendas["steam_fragment"] = sres;

                    list.Add(j);
                }
                catch
                {
                    // Ignorar nodos mal formados
                }
            }

            return list;
        }

        /// <summary>
        /// Guarda los resultados en formato JSON
        /// </summary>
        public static void GuardarResultadosComoJson(List<Juego> juegos, string path = "resultados.json")
        {
            var json = JsonConvert.SerializeObject(juegos, Formatting.Indented);
            System.IO.File.WriteAllText(path, json);
        }

        /// <summary>
        /// BONUS: Versión alternativa con control de velocidad configurable
        /// </summary>
        public static async Task<List<Juego>> ObtenerTopNSteamConVelocidadAsync(
            int n = 200,
            int perPage = 50,
            int delayMs = 600,
            int maxConcurrencia = 4) // ⚡ Configurable
        {
            // Crear semáforo dinámico
            var semaphore = new SemaphoreSlim(maxConcurrencia, maxConcurrencia);

            int pages = (int)Math.Ceiling((double)n / perPage);
            var pageStarts = Enumerable.Range(0, pages).Select(i => i * perPage).ToList();

            var tareas = pageStarts.Select(async (start, index) =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await Task.Delay(index * delayMs / maxConcurrencia);
                    var html = await FetchSteamSearchFragmentAsync(start, perPage);
                    var parsed = ParseSteamSearchFragment(html);
                    await Task.Delay(delayMs);
                    return parsed;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Página {start}: {ex.Message}");
                    return new List<Juego>();
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            var resultados = await Task.WhenAll(tareas);
            var todos = resultados.SelectMany(r => r).Take(n).ToList();

            return todos;
        }
    }
}