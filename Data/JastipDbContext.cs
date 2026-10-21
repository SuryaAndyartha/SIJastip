using Microsoft.EntityFrameworkCore;
using JastipApp.Models;

namespace JastipApp.Data;

public class JastipDbContext : DbContext
{
    /// <summary>Folder data aplikasi, mis. %LocalAppData%\Jastiku di Windows.</summary>
    /// <remarks>Bisa dialihkan lewat environment variable SIJASTIP_DATA_DIR (dipakai oleh proyek uji).</remarks>
    public static string Folder { get; } =
        Environment.GetEnvironmentVariable("SIJASTIP_DATA_DIR") is { Length: > 0 } dir
            ? dir
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
                                                     Environment.SpecialFolderOption.Create), "Jastiku");

    public static string DbPath => Path.Combine(Folder, "jastip.db");

    public DbSet<Pengguna> Pengguna => Set<Pengguna>();
    public DbSet<JastipEvent> Events => Set<JastipEvent>();
    public DbSet<Pesanan> Pesanan => Set<Pesanan>();
    public DbSet<Pembayaran> Pembayaran => Set<Pembayaran>();
    public DbSet<Aktivitas> Aktivitas => Set<Aktivitas>();

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        Directory.CreateDirectory(Folder);
        options.UseSqlite($"Data Source={DbPath}");
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Pengguna>(e =>
        {
            e.HasIndex(p => p.Username).IsUnique();
            e.Property(p => p.Username).HasMaxLength(30).IsRequired();
            e.Property(p => p.Nama).HasMaxLength(60).IsRequired();
        });

        b.Entity<JastipEvent>(e =>
        {
            e.Property(x => x.Judul).HasMaxLength(120).IsRequired();

            // Penyedia tidak boleh dihapus selama masih punya event.
            e.HasOne(x => x.Penyedia).WithMany()
             .HasForeignKey(x => x.PenyediaId)
             .OnDelete(DeleteBehavior.Restrict);

            // Barang ikut terhapus bila event dihapus.
            e.HasMany(x => x.Items).WithOne()
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Pesanan>(e =>
        {
            e.HasOne(p => p.Event).WithMany()
             .HasForeignKey(p => p.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(p => p.Pemesan).WithMany()
             .HasForeignKey(p => p.PemesanId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Detail).WithOne()
             .HasForeignKey(d => d.PesananId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.Pembayaran).WithOne()
             .HasForeignKey(x => x.PesananId)
             .OnDelete(DeleteBehavior.Cascade);

            e.Property(p => p.Kode).HasMaxLength(20).IsRequired();
            e.HasIndex(p => p.Kode).IsUnique();
            e.HasIndex(p => new { p.EventId, p.PemesanId });
        });

        b.Entity<Pembayaran>(e =>
        {
            e.Property(x => x.Metode).HasMaxLength(30);
            e.ToTable(t => t.HasCheckConstraint("CK_Pembayaran_Jumlah", "Jumlah > 0"));
        });

        b.Entity<Aktivitas>(e =>
        {
            e.HasIndex(x => x.Waktu);
        });
    }
}
