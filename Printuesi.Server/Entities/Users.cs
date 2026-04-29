using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;

namespace Printuesi.Server.Entities
{
    public class Users
    {
        [Key]
        public string UserID { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(256)]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public UserRole Role { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<PrintJobs> PrintJobs { get; set; } = new List<PrintJobs>();
    }
}
