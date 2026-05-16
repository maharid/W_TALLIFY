using Microsoft.EntityFrameworkCore;

namespace ProjectTallify.Models
{
    public class TallifyDbContext : DbContext
    {
        public TallifyDbContext(DbContextOptions<TallifyDbContext> options)
            : base(options)
        { }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Event> Events { get; set; } = null!;

        // 👉 NEW ONES:
        public DbSet<Contestant> Contestants { get; set; } = null!;
        public DbSet<Judge> Judges { get; set; } = null!;
        public DbSet<Round> Rounds { get; set; } = null!;
        public DbSet<Criteria> Criterias { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<NotificationLog> NotificationLogs { get; set; } = null!;

        // 👉 SCORING TABLES:
        public DbSet<Score> Scores { get; set; } = null!;
        public DbSet<ComputedRoundScore> ComputedRoundScores { get; set; } = null!;
        public DbSet<OverallScore> OverallScores { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Criteria>()
                .HasOne(c => c.DerivedFromRound)
                .WithMany() 
                .HasForeignKey(c => c.DerivedFromRoundId)
                .OnDelete(DeleteBehavior.Restrict); 
        }
    }
}
