using System.Collections.Generic;

namespace ProyectoParalelismoReal.Models
{
    public class Juego
    {
        public string Nombre { get; set; } = string.Empty;
        public string FuenteInicialUrl { get; set; } = string.Empty;
        public Dictionary<string, StoreResult> Tiendas { get; set; } = new();
        public AnalisisDatos? Analisis { get; set; } = null;
        public string? ImagenUrl { get; set; } = null;
    }

    

    public class AnalisisDatos
    {
        /// <summary>
        /// Calificación de Metacritic (0-100) o promedio de Steam
        /// </summary>
        public double? Calificacion { get; set; }

        /// <summary>
        /// Horas promedio para completar el juego (de HLTB)
        /// </summary>
        public double? HorasPromedio { get; set; }

        /// <summary>
        /// Cantidad de reseñas (Metacritic o Steam)
        /// </summary>
        public int? CantidadResenas { get; set; }

        /// <summary>
        /// Porcentaje de reseñas positivas en Steam (0-100)
        /// </summary>
        public double? CalificacionUsuarios { get; set; }

        /// <summary>
        /// Obtiene un resumen legible del análisis
        /// </summary>
        public string ObtenerResumen()
        {
            var partes = new List<string>();

            if (Calificacion.HasValue)
                partes.Add($"Score: {Calificacion:F0}/100");

            if (CalificacionUsuarios.HasValue)
                partes.Add($"Steam: {CalificacionUsuarios:F0}%+");

            if (HorasPromedio.HasValue)
                partes.Add($"~{HorasPromedio:F1}h");

            if (CantidadResenas.HasValue)
                partes.Add($"{CantidadResenas:N0} reviews");

            return partes.Count > 0 ? string.Join(" | ", partes) : "Sin datos";
        }
    }
}