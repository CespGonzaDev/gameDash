namespace ProyectoParalelismoReal.Models
{
    public class StoreResult
    {
        public string StoreName { get; set; } = "";
        public string? Url { get; set; }
        public string? PriceRaw { get; set; }
        public double? PriceNumber { get; set; }
        public string? ExtraInfo { get; set; }
    }
}
