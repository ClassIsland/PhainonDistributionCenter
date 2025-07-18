using Microsoft.EntityFrameworkCore;
using PhainonDistributionCenter.Entities;

namespace PhainonDistributionCenter;

public class MainDbContext(DbContextOptions<MainDbContext> options) : DbContext(options)
{
    public DbSet<DistributionChannel> DistributionChannels { get; set; }
    public DbSet<VersionInfo> VersionInfos { get; set; }
    public DbSet<DistributionInfo> DistributionInfos { get; set; }
    public DbSet<DistributionSubChannel> DistributionSubChannels { get; set; }
    public DbSet<FileMapInfo> FileMapInfos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        
        base.OnConfiguring(optionsBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DistributionChannel>()
            .HasMany(x => x.AssociatedDistributions)
            .WithMany(x => x.Channels);
        base.OnModelCreating(modelBuilder);
    }
}