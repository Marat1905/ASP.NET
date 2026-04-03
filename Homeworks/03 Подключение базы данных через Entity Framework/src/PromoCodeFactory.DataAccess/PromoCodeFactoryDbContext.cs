using Microsoft.EntityFrameworkCore;
using PromoCodeFactory.Core.Domain.Administration;
using PromoCodeFactory.Core.Domain.PromoCodeManagement;

namespace PromoCodeFactory.DataAccess;

public class PromoCodeFactoryDbContext : DbContext
{
    public PromoCodeFactoryDbContext(DbContextOptions<PromoCodeFactoryDbContext> options)
        : base(options) { }

    public DbSet<Employee> Employees { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Preference> Preferences { get; set; }
    public DbSet<PromoCode> PromoCodes { get; set; }
    public DbSet<CustomerPromoCode> CustomerPromoCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Employee
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasOne(e => e.Role)
                .WithMany()
                .HasForeignKey("RoleId")
                .IsRequired();
        });

        // Role
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(100);
            entity.Property(r => r.Description).HasMaxLength(500);
        });

        // Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(256);
            entity.HasMany(c => c.Preferences)
                .WithMany(p => p.Customers)
                .UsingEntity(j => j.ToTable("CustomerPreferences"));
            entity.HasMany(c => c.CustomerPromoCodes)
                .WithOne()
                .HasForeignKey(cpc => cpc.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Preference
        modelBuilder.Entity<Preference>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(100);
        });

        // PromoCode
        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).IsRequired().HasMaxLength(256);
            entity.Property(p => p.ServiceInfo).IsRequired().HasMaxLength(256);
            entity.Property(p => p.PartnerName).IsRequired().HasMaxLength(256);
            entity.Property(p => p.BeginDate).IsRequired();
            entity.Property(p => p.EndDate).IsRequired();
            entity.HasOne(p => p.PartnerManager)
                .WithMany()
                .HasForeignKey("PartnerManagerId")
                .IsRequired();
            entity.HasOne(p => p.Preference)
                .WithMany()
                .HasForeignKey("PreferenceId")
                .IsRequired();
            entity.HasMany(p => p.CustomerPromoCodes)
                .WithOne()
                .HasForeignKey(cpc => cpc.PromoCodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CustomerPromoCode
        modelBuilder.Entity<CustomerPromoCode>(entity =>
        {
            entity.HasKey(cpc => cpc.Id);
            entity.Property(cpc => cpc.CreatedAt).IsRequired();
            entity.Property(cpc => cpc.AppliedAt);
        });

        base.OnModelCreating(modelBuilder);
    }
}
