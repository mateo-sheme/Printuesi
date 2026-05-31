// Entities/PrinterState.cs
using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class PrinterState
    {
        [Key]
        public int Id { get; set; } = 1; // always single row
        public PaperSize? CurrentPaperSize { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}