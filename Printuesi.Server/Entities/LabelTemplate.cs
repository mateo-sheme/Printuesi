using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class LabelTemplate
    {
        [Key]
        public string Label_ID { get; set; }

        [Required]
        public string Client_ID { get; set; } = string.Empty;

        [Required]
        public PaperSize PaperSize { get; set; }

        [Required]
        public string Label_Name { get; set; } = string.Empty;

        [Required]
        public string Pdf_Path { get; set; } = string.Empty;

        public required string Date_Format { get; set; }          // "dd/MM/yyyy" or "MM/yyyy"
        public int Expiry_Offset_Months { get; set; }

        [ForeignKey("Client_ID")]
        public Clients Clients { get; set; }
    }
}
