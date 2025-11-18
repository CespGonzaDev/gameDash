using HtmlAgilityPack;
using ProyectoParalelismoReal.Models;
using PuppeteerSharp;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProyectoParalelismoReal.Services
{
    /// <summary>
    /// VERSIÓN DEFINITIVA - Steam + GOG + Fanatical
    /// GOG es MÁS CONFIABLE que CDKeys (tiene API pública)
    /// </summary>
    public static class ComparadorTiendas
    {
        private static readonly HttpClient _http = new HttpClient();
        private static IBrowser? _browser;
        private static readonly SemaphoreSlim _browserSemaphore = new SemaphoreSlim(10, 10);
        private static readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);
        private static readonly Random _random = new Random();
        private static bool _browserInitialized = false;

        private static int _puppeteerUsado = 0;
        private static int _httpUsado = 0;
        private static int _preciosExtraidosHTTP = 0;
        private static int _preciosExtraidosPuppeteer = 0;

        static ComparadorTiendas()
        {
            _http.Timeout = TimeSpan.FromSeconds(12);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        }

        private static async Task InicializarBrowserAsync()
        {
            if (_browserInitialized) return;

            await _initSemaphore.WaitAsync();
            try
            {
                if (_browserInitialized) return;

                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync();

                _browser = await Puppeteer.LaunchAsync(new LaunchOptions
                {
                    Headless = true,
                    Args = new[]
                    {
                        "--no-sandbox",
                        "--disable-setuid-sandbox",
                        "--disable-dev-shm-usage",
                        "--disable-gpu",
                        "--disable-images",
                        "--blink-settings=imagesEnabled=false",
                        "--window-size=1280,720"
                    }
                });

                _browserInitialized = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        /// <summary>
        /// ⚡ CONFIGURACIÓN FINAL: Steam + GOG + Fanatical
        /// </summary>
        public static async Task<Dictionary<string, StoreResult>> BuscarEnTiendasAsync(string nombreJuego)
        {
            var taskSteam = BuscarSteam(nombreJuego);
            var taskEneba = BuscarEneba(nombreJuego);  // ⚡ CAMBIADO
            var taskFanatical = BuscarFanaticalPuppeteer(nombreJuego);

            await Task.WhenAll(taskSteam, taskEneba, taskFanatical);

            var resultados = new Dictionary<string, StoreResult>();

            var steam = await taskSteam;
            if (steam.Item2 != null) resultados[steam.Item1] = steam.Item2;

            var eneba = await taskEneba;  // ⚡ CAMBIADO
            if (eneba.Item2 != null) resultados[eneba.Item1] = eneba.Item2;

            var fanatical = await taskFanatical;
            if (fanatical.Item2 != null) resultados[fanatical.Item1] = fanatical.Item2;

            return resultados;
        }

        private static async Task<(string, StoreResult?)> BuscarSteam(string nombre)
        {
            try
            {
                await Task.Delay(_random.Next(30, 80));

                string q = Uri.EscapeDataString(nombre);
                string url = $"https://store.steampowered.com/search/?term={q}&cc=US&l=english";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var response = await _http.GetAsync(url, cts.Token);

                if (!response.IsSuccessStatusCode) return ("steam", null);

                string html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var rows = doc.DocumentNode.SelectNodes("//a[contains(@class,'search_result_row')]");
                if (rows == null || !rows.Any()) return ("steam", null);

                foreach (var row in rows.Take(2))
                {
                    var precioDescuentoNodo = row.SelectSingleNode(".//div[contains(@class,'discount_final_price')]");
                    var precioNodo = precioDescuentoNodo ?? row.SelectSingleNode(".//div[contains(@class,'search_price')]");

                    if (precioNodo == null) continue;

                    var txt = precioNodo.InnerText.Trim();

                    if (txt.Contains("Free", StringComparison.OrdinalIgnoreCase))
                    {
                        Interlocked.Increment(ref _httpUsado);
                        Interlocked.Increment(ref _preciosExtraidosHTTP);
                        return ("steam", new StoreResult
                        {
                            StoreName = "Steam",
                            Url = row.GetAttributeValue("href", ""),
                            PriceRaw = "Free",
                            PriceNumber = 0.0,
                            ExtraInfo = "Steam"
                        });
                    }

                    var precio = ExtraerPrecio(txt);
                    if (precio.HasValue)
                    {
                        Interlocked.Increment(ref _httpUsado);
                        Interlocked.Increment(ref _preciosExtraidosHTTP);
                        return ("steam", new StoreResult
                        {
                            StoreName = "Steam",
                            Url = row.GetAttributeValue("href", ""),
                            PriceRaw = txt,
                            PriceNumber = precio,
                            ExtraInfo = "Steam"
                        });
                    }
                }

                return ("steam", null);
            }
            catch
            {
                return ("steam", null);
            }
        }

        /// <summary>
        /// ⚡ GOG.COM - API pública + HTML simple
        /// Tasa de éxito esperada: 60-70%
        /// </summary>
        private static async Task<(string, StoreResult?)> BuscarEneba(string nombre)
        {
            try
            {
                await Task.Delay(_random.Next(50, 120));

                var nombreLimpio = LimpiarNombreJuego(nombre);
                var encoded = Uri.EscapeDataString(nombreLimpio);

                // Eneba tiene búsqueda simple
                var url = $"https://www.eneba.com/us/store?text={encoded}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                // Headers adicionales para Eneba
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept-Language", "en-US,en;q=0.9");

                using var response = await _http.SendAsync(request, cts.Token);

                if (!response.IsSuccessStatusCode) return ("eneba", null);

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Eneba usa clases dinámicas, buscar por múltiples selectores
                var products = doc.DocumentNode.SelectNodes(
                    "//div[contains(@class,'product')] | " +
                    "//a[contains(@class,'product')] | " +
                    "//div[contains(@data-test,'product')]");

                if (products == null || !products.Any()) return ("eneba", null);

                var product = products.First();

                // Buscar precio con múltiples intentos
                var priceNode = product.SelectSingleNode(
                    ".//span[contains(@class,'price')] | " +
                    ".//div[contains(@class,'price')] | " +
                    ".//span[contains(@data-test,'price')]");

                if (priceNode == null) return ("eneba", null);

                var priceText = priceNode.InnerText.Trim();

                if (priceText.Contains("Free", StringComparison.OrdinalIgnoreCase))
                {
                    var linkNode = product.Name == "a" ? product : product.SelectSingleNode(".//a[@href]");
                    var href = linkNode?.GetAttributeValue("href", "");

                    Interlocked.Increment(ref _httpUsado);
                    Interlocked.Increment(ref _preciosExtraidosHTTP);

                    return ("eneba", new StoreResult
                    {
                        StoreName = "Eneba",
                        Url = href?.StartsWith("http") == true ? href : $"https://www.eneba.com{href}",
                        PriceRaw = "Free",
                        PriceNumber = 0.0,
                        ExtraInfo = "Eneba"
                    });
                }

                var precio = ExtraerPrecio(priceText);

                if (precio.HasValue)
                {
                    var linkNode = product.Name == "a" ? product : product.SelectSingleNode(".//a[@href]");
                    var href = linkNode?.GetAttributeValue("href", "");

                    Interlocked.Increment(ref _httpUsado);
                    Interlocked.Increment(ref _preciosExtraidosHTTP);

                    return ("eneba", new StoreResult
                    {
                        StoreName = "Eneba",
                        Url = href?.StartsWith("http") == true ? href : $"https://www.eneba.com{href}",
                        PriceRaw = priceText,
                        PriceNumber = precio,
                        ExtraInfo = "Eneba"
                    });
                }

                return ("eneba", null);
            }
            catch
            {
                return ("eneba", null);
            }
        }

        /// <summary>
        /// FANATICAL - Solo Puppeteer (HTTP no funciona bien)
        /// </summary>
        private static async Task<(string, StoreResult?)> BuscarFanaticalPuppeteer(string nombre)
        {
            await _browserSemaphore.WaitAsync();
            IPage? page = null;
            try
            {
                await InicializarBrowserAsync();
                if (_browser == null) return ("fanatical", null);

                page = await _browser.NewPageAsync();
                await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

                var nombreLimpio = LimpiarNombreJuego(nombre);
                var encoded = Uri.EscapeDataString(nombreLimpio);
                var url = $"https://www.fanatical.com/en/search?search={encoded}";

                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2 },
                    Timeout = 15000
                });

                // Esperar a que carguen los productos
                await Task.Delay(2000);

                var resultado = await page.EvaluateFunctionAsync<PriceResult>(@"() => {
                    // Intentar múltiples selectores
                    let products = Array.from(document.querySelectorAll(
                        '[class*=""card""], ' +
                        '.product-card, ' +
                        'article, ' +
                        '[data-test*=""product""]'
                    ));
                    
                    if (products.length === 0) return { found: false };

                    // Buscar en los primeros 3 productos
                    for (const product of products.slice(0, 3)) {
                        const priceSelectors = [
                            '[class*=""price""]',
                            '.price',
                            '[data-price]',
                            'span[class*=""Price""]'
                        ];
                        
                        let priceEl = null;
                        for (const selector of priceSelectors) {
                            priceEl = product.querySelector(selector);
                            if (priceEl) break;
                        }
                        
                        if (!priceEl) continue;
                        
                        const priceText = priceEl.textContent.trim();
                        
                        if (!priceText || !/\d/.test(priceText)) continue;

                        const link = product.querySelector('a');
                        
                        return {
                            price: priceText,
                            url: link?.href || '',
                            found: true
                        };
                    }
                    
                    return { found: false };
                }");

                if (resultado?.Found == true)
                {
                    var precio = ExtraerPrecio(resultado.Price);
                    if (precio.HasValue)
                    {
                        Interlocked.Increment(ref _puppeteerUsado);
                        Interlocked.Increment(ref _preciosExtraidosPuppeteer);
                        return ("fanatical", new StoreResult
                        {
                            StoreName = "Fanatical",
                            Url = resultado.Url,
                            PriceRaw = resultado.Price,
                            PriceNumber = precio,
                            ExtraInfo = "Fanatical-JS"
                        });
                    }
                }

                return ("fanatical", null);
            }
            catch
            {
                return ("fanatical", null);
            }
            finally
            {
                if (page != null) try { await page.CloseAsync(); } catch { }
                _browserSemaphore.Release();
            }
        }

        private static double? ExtraerPrecio(string priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText)) return null;

            try
            {
                if (priceText.Contains("Free", StringComparison.OrdinalIgnoreCase))
                    return 0.0;

                var cleaned = priceText
                    .Replace("USD", "")
                    .Replace("€", "")
                    .Replace("£", "")
                    .Replace("₹", "")
                    .Replace("¥", "")
                    .Trim();

                var matches = System.Text.RegularExpressions.Regex.Matches(
                    cleaned,
                    @"\$?\s*(\d+\.\d{2})"
                );

                if (matches.Count == 0)
                {
                    matches = System.Text.RegularExpressions.Regex.Matches(
                        cleaned,
                        @"\$?\s*(\d+,\d{2})"
                    );
                }

                if (matches.Count == 0) return null;

                var precios = new List<double>();
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var valor = match.Groups[1].Value.Replace(",", ".");
                    if (double.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
                    {
                        if (p >= 0.01 && p <= 999.99)
                            precios.Add(p);
                    }
                }

                if (precios.Any())
                {
                    return precios.Min();
                }
            }
            catch { }

            return null;
        }

        private static string LimpiarNombreJuego(string nombre)
        {
            return nombre
                .Replace(" - Deluxe Edition", "")
                .Replace(" - Standard Edition", "")
                .Replace(" - Ultimate Edition", "")
                .Replace(" Deluxe", "")
                .Replace(" Digital Deluxe", "")
                .Replace("™", "").Replace("®", "")
                .Replace(" - Upgrade to Deluxe Edition", "")
                .Trim();
        }

        public static async Task CerrarBrowserAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync();
                _browser = null;
                _browserInitialized = false;
            }

            var total = _httpUsado + _puppeteerUsado;
            Console.WriteLine($"\n📊 Estadísticas de scraping:");
            Console.WriteLine($"   • Requests HTTP: {_httpUsado}/{total} ({(_httpUsado * 100.0 / Math.Max(total, 1)):F0}%)");
            Console.WriteLine($"   • Requests Puppeteer: {_puppeteerUsado}/{total} ({(_puppeteerUsado * 100.0 / Math.Max(total, 1)):F0}%)");
            Console.WriteLine($"   • Precios extraídos (HTTP): {_preciosExtraidosHTTP}");
            Console.WriteLine($"   • Precios extraídos (Puppeteer): {_preciosExtraidosPuppeteer}");
        }

        private class PriceResult
        {
            public string Price { get; set; } = "";
            public string Url { get; set; } = "";
            public bool Found { get; set; }
        }
    }
}