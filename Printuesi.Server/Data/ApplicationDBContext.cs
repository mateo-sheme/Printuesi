using Microsoft.EntityFrameworkCore;

namespace Printuesi.Server.Data
{
    public class ApplicationDBContext : DbContext
    {
        public ApplicationDBContext(DbContextOptions options) : base(options)
        {
        }

   
    }
}
