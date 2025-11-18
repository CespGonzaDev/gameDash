using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ProyectoParalelismoReal.Models;
using ProyectoParalelismoReal.Services;

namespace ProyectoParalelismoReal
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   🚀 PROYECTO PARALELISMO - VERSIÓN ULTRA OPTIMIZADA 🚀   ║");
            Console.WriteLine("║              Objetivo: 200 juegos en < 3 minutos          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

            var sw = Stopwatch.StartNew();

            int n = 200;
            int maxParalelismo = 10;

            Console.WriteLine($"⚙️  Configuración FINAL OPTIMIZADA:");
            Console.WriteLine($"   • Juegos a obtener: {n}");
            Console.WriteLine($"   • Paralelismo: {maxParalelismo} juegos simultáneos");
            Console.WriteLine($"   • Estrategia: HTTP primero (70% más rápido)");
            Console.WriteLine($"   • Fallback: Puppeteer si HTTP falla");
            Console.WriteLine($"   • Target: < 3 minutos\n");

            // NIVEL 1: Obtener juegos de Steam
            Console.WriteLine("📊 [NIVEL 1] Obteniendo top 200 juegos desde Steam...");
            var swNivel1 = Stopwatch.StartNew();
            var topJuegos = await ScraperJuegos.ObtenerTopNSteamAsync(n: n, perPage: 50, delayMs: 300);
            swNivel1.Stop();
            Console.WriteLine($"✓ [NIVEL 1] {topJuegos.Count} juegos obtenidos en {swNivel1.Elapsed.TotalSeconds:F1}s\n");

            // NIVEL 2 y 3: Procesamiento paralelo AGRESIVO
            Console.WriteLine($"🔥 [NIVEL 2+3] Procesando {topJuegos.Count} juegos con paralelismo MÁXIMO...\n");

            var swNivel23 = Stopwatch.StartNew();
            var options = new ParallelOptions { MaxDegreeOfParallelism = maxParalelismo };
            var resultados = new ConcurrentBag<Juego>();
            var errores = new ConcurrentBag<string>();
            int procesados = 0;

            await Parallel.ForEachAsync(topJuegos, options, async (juego, token) =>
            {
                try
                {
                    bool esHardware = EsHardware(juego);

                    if (esHardware)
                    {
                        juego.Tiendas = CrearResultadoHardware(juego);
                        juego.Analisis = null;
                    }
                    else
                    {
                        // ⚡ OPTIMIZACIÓN: Extraer Steam App ID para usar API rápida de HLTB
                        var steamAppId = AnalizadorDatos.ExtraerSteamAppId(juego.FuenteInicialUrl);
                        if (!string.IsNullOrEmpty(steamAppId))
                        {
                            juego.ImagenUrl =
                                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{steamAppId}/capsule_231x87.jpg";
                        }
                        else
                        {
                            juego.ImagenUrl = "img/no-image.png"; // una imagen genérica local
                        }



                        var taskStores = ComparadorTiendas.BuscarEnTiendasAsync(juego.Nombre);

                        // Si tenemos Steam App ID, usar la API rápida
                        Task<AnalisisDatos> taskAnalisis;
                        if (!string.IsNullOrEmpty(steamAppId))
                        {
                            taskAnalisis = Task.Run(async () =>
                            {
                                var hltb = await AnalizadorDatos.BuscarEnHowLongToBeatConSteamId(steamAppId, juego.Nombre);
                                var steamStats = await AnalizadorDatos.ObtenerEstadisticasSteamAsync(juego.Nombre);

                                return new AnalisisDatos
                                {
                                    Calificacion = steamStats?.CalificacionPromedio,
                                    CantidadResenas = steamStats?.NumeroResenas,
                                    HorasPromedio = hltb?.HorasPromedio,
                                    CalificacionUsuarios = steamStats?.PorcentajePositivas
                                };
                            });
                        }
                        else
                        {
                            taskAnalisis = AnalizadorDatos.AnalizarJuegoAsync(juego.Nombre);
                        }

                        await Task.WhenAll(taskStores, taskAnalisis);

                        juego.Tiendas = await taskStores;
                        juego.Analisis = await taskAnalisis;
                    }

                    resultados.Add(juego);

                    var p = System.Threading.Interlocked.Increment(ref procesados);
                    if (p % 10 == 0)
                    {
                        Console.WriteLine($"⚡ Progreso: {p}/{topJuegos.Count} juegos ({(p * 100.0 / topJuegos.Count):F0}%) - Tiempo: {sw.Elapsed.TotalSeconds:F1}s");
                    }
                }
                catch (Exception ex)
                {
                    errores.Add($"{juego.Nombre}: {ex.Message}");
                }
            });

            swNivel23.Stop();
            sw.Stop();

            await ComparadorTiendas.CerrarBrowserAsync();

            // Mostrar estadísticas de HLTB
            AnalizadorDatos.MostrarEstadisticasHLTB();

            // Resumen final
            Console.WriteLine("\n" + new string('═', 60));
            Console.WriteLine("🏆 RESUMEN FINAL - VERSIÓN OPTIMIZADA");
            Console.WriteLine(new string('═', 60));
            Console.WriteLine($"✓ Juegos procesados: {resultados.Count}/{topJuegos.Count}");
            Console.WriteLine($"⏱️  Tiempo total: {sw.Elapsed.TotalMinutes:F2} minutos ({sw.Elapsed.TotalSeconds:F1}s)");
            Console.WriteLine($"   └─ Nivel 1 (Steam): {swNivel1.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"   └─ Nivel 2+3 (Tiendas + Análisis): {swNivel23.Elapsed.TotalSeconds:F1}s");

            if (resultados.Count > 0)
            {
                // ⚡ CORRECCIÓN: Calcular tiempo promedio usando el tiempo de Nivel 2+3
                var tiempoPorJuego = swNivel23.Elapsed.TotalSeconds / resultados.Count;
                Console.WriteLine($"⚡ Tiempo promedio por juego (Nivel 2+3): {tiempoPorJuego:F2}s");

                // Estimación con la versión anterior (sin paralelismo optimizado)
                var tiempoEstimadoAnterior = resultados.Count * 2.7; // 2.7s por juego
                var speedup = tiempoEstimadoAnterior / swNivel23.Elapsed.TotalSeconds;
                Console.WriteLine($"🚀 Speedup vs versión secuencial: {speedup:F1}x más rápido");

                // Throughput (juegos por segundo)
                var throughput = resultados.Count / swNivel23.Elapsed.TotalSeconds;
                Console.WriteLine($"📈 Throughput: {throughput:F2} juegos/segundo");

                if (sw.Elapsed.TotalMinutes < 3)
                {
                    Console.WriteLine($"\n✅ ¡META ALCANZADA! Procesado en {sw.Elapsed.TotalMinutes:F2} min (< 3 min)");
                }
                else
                {
                    Console.WriteLine($"\n⚠️  Objetivo no alcanzado: {sw.Elapsed.TotalMinutes:F2} min (meta: < 3 min)");
                    var diferencia = sw.Elapsed.TotalMinutes - 3;
                    Console.WriteLine($"   Faltaron {diferencia:F2} minutos");

                    // Recomendación basada en el tiempo
                    if (diferencia < 0.5)
                    {
                        Console.WriteLine($"   💡 Sugerencia: Aumentar MaxDegreeOfParallelism a {maxParalelismo + 2}");
                    }
                    else
                    {
                        Console.WriteLine($"   💡 Sugerencia: Aumentar MaxDegreeOfParallelism a {maxParalelismo + 5}");
                    }
                }
            }

            if (errores.Any())
            {
                Console.WriteLine($"\n⚠️  Errores: {errores.Count} ({(errores.Count * 100.0 / topJuegos.Count):F1}%)");

                // Mostrar primeros 3 errores
                Console.WriteLine("   Primeros errores:");
                foreach (var error in errores.Take(3))
                {
                    Console.WriteLine($"   • {error}");
                }
            }

            // Guardar resultados
            Console.WriteLine($"\n💾 Guardando {resultados.Count} juegos en 'resultados.json'...");
            ScraperJuegos.GuardarResultadosComoJson(resultados.OrderBy(j => j.Nombre).ToList(), "resultados.json");
            Console.WriteLine("✓ Guardado exitoso");

            // Estadísticas de tiendas
            MostrarEstadisticasTiendas(resultados.ToList());

            // Top 5 mejores precios
            MostrarTop5MejoresPrecios(resultados.ToList());

            // Análisis de calidad de datos
            MostrarAnalisisCalidadDatos(resultados.ToList());

            Console.WriteLine("\n✅ Proceso finalizado. Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }

        private static bool EsHardware(Juego juego)
        {
            return juego.Nombre.Contains("Steam Deck", StringComparison.OrdinalIgnoreCase) ||
                   juego.Nombre.Contains("Steam Controller", StringComparison.OrdinalIgnoreCase) ||
                   juego.FuenteInicialUrl.Contains("/steamdeck", StringComparison.OrdinalIgnoreCase);
        }

        private static System.Collections.Generic.Dictionary<string, StoreResult> CrearResultadoHardware(Juego juego)
        {
            return new System.Collections.Generic.Dictionary<string, StoreResult>
            {
                ["steam"] = new StoreResult
                {
                    StoreName = "Steam",
                    Url = juego.FuenteInicialUrl,
                    PriceRaw = "Hardware",
                    PriceNumber = null,
                    ExtraInfo = "Hardware"
                }
            };
        }

        private static void MostrarEstadisticasTiendas(System.Collections.Generic.List<Juego> juegos)
        {
            Console.WriteLine("\n" + new string('─', 60));
            Console.WriteLine("📊 ESTADÍSTICAS DE TIENDAS");
            Console.WriteLine(new string('─', 60));

            var totalJuegos = juegos.Count;
            var juegosConSteam = juegos.Count(j => j.Tiendas.ContainsKey("steam") && j.Tiendas["steam"].PriceNumber.HasValue);
            var juegosConEneba = juegos.Count(j => j.Tiendas.ContainsKey("eneba") && j.Tiendas["eneba"].PriceNumber.HasValue);
            var juegosConFanatical = juegos.Count(j => j.Tiendas.ContainsKey("fanatical") && j.Tiendas["fanatical"].PriceNumber.HasValue);

            Console.WriteLine($"Steam:     {juegosConSteam}/{totalJuegos} ({(juegosConSteam * 100.0 / totalJuegos):F0}%) con precio");
            Console.WriteLine($"Eneba:     {juegosConEneba}/{totalJuegos} ({(juegosConEneba * 100.0 / totalJuegos):F0}%) con precio");
            Console.WriteLine($"Fanatical: {juegosConFanatical}/{totalJuegos} ({(juegosConFanatical * 100.0 / totalJuegos):F0}%) con precio");

            var conVariasTiendas = juegos.Count(j =>
                j.Tiendas.Count(kv => kv.Value.PriceNumber.HasValue) >= 2);
            Console.WriteLine($"\nJuegos en 2+ tiendas: {conVariasTiendas} ({(conVariasTiendas * 100.0 / totalJuegos):F0}%)");
        }

        private static void MostrarTop5MejoresPrecios(System.Collections.Generic.List<Juego> juegos)
        {
            Console.WriteLine("\n" + new string('─', 60));
            Console.WriteLine("💎 TOP 5 MEJORES PRECIOS");
            Console.WriteLine(new string('─', 60));

            var conPrecio = juegos
                .Where(j => j.Tiendas.Values.Any(s => s.PriceNumber.HasValue && s.PriceNumber > 0))
                .Select(j => new
                {
                    Juego = j,
                    MejorPrecio = j.Tiendas.Values
                        .Where(s => s.PriceNumber.HasValue && s.PriceNumber > 0)
                        .OrderBy(s => s.PriceNumber)
                        .First()
                })
                .OrderBy(x => x.MejorPrecio.PriceNumber)
                .Take(5)
                .ToList();

            if (!conPrecio.Any())
            {
                Console.WriteLine("   (No hay datos de precios disponibles)");
                return;
            }

            int pos = 1;
            foreach (var item in conPrecio)
            {
                Console.WriteLine($"{pos}. {item.Juego.Nombre}");
                Console.WriteLine($"   ${item.MejorPrecio.PriceNumber:F2} en {item.MejorPrecio.StoreName}");
                pos++;
            }
        }

        private static void MostrarAnalisisCalidadDatos(System.Collections.Generic.List<Juego> juegos)
        {
            Console.WriteLine("\n" + new string('─', 60));
            Console.WriteLine("📈 ANÁLISIS DE CALIDAD DE DATOS");
            Console.WriteLine(new string('─', 60));

            var conAnalisis = juegos.Count(j => j.Analisis != null);
            var conHoras = juegos.Count(j => j.Analisis?.HorasPromedio.HasValue == true);
            var conCalificacion = juegos.Count(j => j.Analisis?.Calificacion.HasValue == true);
            var conResenas = juegos.Count(j => j.Analisis?.CantidadResenas.HasValue == true);

            Console.WriteLine($"Juegos con análisis: {conAnalisis}/{juegos.Count} ({(conAnalisis * 100.0 / juegos.Count):F0}%)");
            Console.WriteLine($"  └─ Con horas (HLTB): {conHoras} ({(conHoras * 100.0 / juegos.Count):F0}%)");
            Console.WriteLine($"  └─ Con calificación: {conCalificacion} ({(conCalificacion * 100.0 / juegos.Count):F0}%)");
            Console.WriteLine($"  └─ Con reseñas: {conResenas} ({(conResenas * 100.0 / juegos.Count):F0}%)");

            // Promedios
            if (conHoras > 0)
            {
                var promedioHoras = juegos
                    .Where(j => j.Analisis?.HorasPromedio.HasValue == true)
                    .Average(j => j.Analisis!.HorasPromedio!.Value);
                Console.WriteLine($"\n⏱️  Promedio de horas de juego: {promedioHoras:F1}h");
            }

            if (conCalificacion > 0)
            {
                var promedioCalif = juegos
                    .Where(j => j.Analisis?.Calificacion.HasValue == true)
                    .Average(j => j.Analisis!.Calificacion!.Value);
                Console.WriteLine($"⭐ Calificación promedio: {promedioCalif:F1}/100");
            }
        }
    }
}