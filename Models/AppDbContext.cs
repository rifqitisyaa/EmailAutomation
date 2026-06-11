using Microsoft.EntityFrameworkCore;
using EmailAutomation.Models;

namespace EmailAutomation.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<ReportJobConfig> ReportJobConfigs { get; set; }
        public DbSet<EmailAutomationLog> EmailAutomationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ReportJobConfig>().ToTable("ReportJobConfig");
            modelBuilder.Entity<EmailAutomationLog>().ToTable("EmailAutomationLog");
            modelBuilder.Entity<ReportJobConfig>().HasKey(e => e.Id);
        }
    }
}