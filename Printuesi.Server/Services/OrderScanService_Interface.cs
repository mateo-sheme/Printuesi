using Printuesi.Server.Enums;

namespace Printuesi.Server.Services
{
    public interface OrderScanService_Interface
    {
        /// <summary>
        /// Parses an order PDF and returns extracted line items
        /// </summary>
        Task<OrderScanResult> ScanOrderAsync(IFormFile orderPdf);
    }

    public class OrderScanResult
    {
        public string ClientName { get; set; } = string.Empty;      // Extracted from Marrësi
        public string ClientNameRaw { get; set; } = string.Empty;   // Full raw text from PDF
        public DateTime OrderDate { get; set; }
        public List<OrderLineItem> Items { get; set; } = new();
        public List<string> UnrecognizedCodes { get; set; } = new(); // Codes not in ProductSizeMap
    }

    public class OrderLineItem
    {
        public string ProductCode { get; set; } = string.Empty;     // e.g. STK30
        public string ProductName { get; set; } = string.Empty;     // Full description
        public int Quantity { get; set; }                           // Dërguar column
        public PaperSize PaperSize { get; set; }                    // Resolved from ProductSizeMap
        public bool HasRrethi { get; set; } = false;                // Whether to also print logo label
    }
}