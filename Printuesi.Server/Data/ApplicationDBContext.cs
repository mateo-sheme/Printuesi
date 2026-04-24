using Microsoft.EntityFrameworkCore;
using Printuesi.Server.Entities;

namespace Printuesi.Server.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Users> Users { get; set; }
        public DbSet<Clients> Clients { get; set; }
        public DbSet<LabelTemplate> LabelTemplates { get; set; }
        public DbSet<PrintJobs> PrintJobs { get; set; }
        public DbSet<PrintJobObjects> PrintJobObjects { get; set; }
        public DbSet<Supplies> Supplies { get; set; }
        public DbSet<SupplyUsageLogs> SupplyUsageLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>().Property(u => u.User_ID).ValueGeneratedNever();
            modelBuilder.Entity<Clients>().Property(c => c.Client_ID).ValueGeneratedNever();
            modelBuilder.Entity<LabelTemplate>().Property(l => l.Label_ID).ValueGeneratedNever();
            modelBuilder.Entity<PrintJobs>().Property(p => p.PrintJob_ID).ValueGeneratedNever();
            modelBuilder.Entity<PrintJobObjects>().Property(p => p.PrintJobObject_ID).ValueGeneratedNever();
            modelBuilder.Entity<Supplies>().Property(s => s.Supply_ID).ValueGeneratedNever();
            modelBuilder.Entity<SupplyUsageLogs>().Property(s => s.SupplyUsageLog_ID).ValueGeneratedNever();

            modelBuilder.Entity<PrintJobs>()
                .HasOne(p => p.User)
                .WithMany(u => u.PrintJobs)
                .HasForeignKey(p => p.Created_By)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobs>()
                .HasOne(p => p.Client)
                .WithMany(c => c.PrintJobs)
                .HasForeignKey(p => p.Client_ID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobObjects>()
                .HasOne(p => p.PrintJob)
                .WithMany(j => j.PrintJobObjects)
                .HasForeignKey(p => p.PrintJob_ID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobObjects>()
                .HasOne(p => p.LabelTemplate)
                .WithMany()
                .HasForeignKey(p => p.Label_ID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyUsageLogs>()
                .HasOne(s => s.Supply)
                .WithMany(s => s.UsageLogs)
                .HasForeignKey(s => s.Supply_ID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyUsageLogs>()
                .HasOne(s => s.PrintJob)
                .WithMany()
                .HasForeignKey(s => s.PrintJob_ID)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}