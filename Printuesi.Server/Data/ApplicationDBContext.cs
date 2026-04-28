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
            modelBuilder.Entity<Users>().Property(u => u.UserID).ValueGeneratedNever();
            modelBuilder.Entity<Clients>().Property(c => c.ClientID).ValueGeneratedNever();
            modelBuilder.Entity<LabelTemplate>().Property(l => l.LabelID).ValueGeneratedNever();
            modelBuilder.Entity<PrintJobs>().Property(p => p.PrintJobID).ValueGeneratedNever();
            modelBuilder.Entity<PrintJobObjects>().Property(p => p.PrintJobObjectID).ValueGeneratedNever();
            modelBuilder.Entity<Supplies>().Property(s => s.SupplyID).ValueGeneratedNever();
            modelBuilder.Entity<SupplyUsageLogs>().Property(s => s.SupplyUsageLogID).ValueGeneratedNever();

            modelBuilder.Entity<PrintJobs>()
                .HasOne(p => p.User)
                .WithMany(u => u.PrintJobs)
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobs>()
                .HasOne(p => p.Client)
                .WithMany(c => c.PrintJobs)
                .HasForeignKey(p => p.ClientID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobObjects>()
                .HasOne(p => p.PrintJob)
                .WithMany(j => j.PrintJobObjects)
                .HasForeignKey(p => p.PrintJobID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrintJobObjects>()
                .HasOne(p => p.LabelTemplate)
                .WithMany()
                .HasForeignKey(p => p.LabelID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyUsageLogs>()
                .HasOne(s => s.Supply)
                .WithMany(s => s.UsageLogs)
                .HasForeignKey(s => s.SupplyID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupplyUsageLogs>()
                .HasOne(s => s.PrintJob)
                .WithMany()
                .HasForeignKey(s => s.PrintJobID)
                .OnDelete(DeleteBehavior.NoAction);
        }

    }
}