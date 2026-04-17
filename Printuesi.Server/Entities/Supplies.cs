using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class Supplies
    {
        [Key]
        public string Supply_ID { get; set; }

        [Required]
        public SupplyType SupplyType { get; set; }

        [Required]
        public float Quantity { get; set; }

        [Required]
        public string Unit { get; set; } = string.Empty;

        [Required]
        public DateOnly Added { get; set; }

        public ICollection<SupplyUsageLogs> UsageLogs { get; set; }
    }
}
