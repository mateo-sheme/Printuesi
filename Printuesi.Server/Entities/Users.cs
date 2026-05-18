using System.ComponentModel.DataAnnotations;
using Printuesi.Server.Enums;
using Printuesi.Server.Helpers;

namespace Printuesi.Server.Entities
{
    public class Users
    {
        [Key]
        public string UserID { get; set; } = ID_Generator.User();

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
