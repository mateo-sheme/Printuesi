using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class Supplies
    {
        [Key]
        public Guid SupplyID { get; set; } = Guid.NewGuid();

        [Required]
        public SupplyType SupplyType { get; set; }

        [Required]
        public float Quantity { get; set; }

        [Required]
        public string Unit { get; set; } = string.Empty;

        [Required]
        public DateOnly Added { get; set; }

        public ICollection<SupplyUsageLogs> UsageLogs { get; set; } = new List<SupplyUsageLogs>();
    }
}
