using HRAnalysis.Models;
using Microsoft.EntityFrameworkCore;

namespace HRAnalysis.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<tbl_AttUser> tbl_AttUser { get; set; }
        public DbSet<tbl_ATTKesalahan> tbl_ATTKesalahan { get; set; }
        public DbSet<tbl_ATTSemakan> tbl_ATTSemakan { get; set; }
        public DbSet<tbl_ATTSemakanV1> tbl_ATTSemakanV1 { get; set; }
        public DbSet<v_HRA_AttKesalahan> v_HRA_AttKesalahan { get; set; }
        public DbSet<v_HRA_ATTSemakan_CutiUmum> v_HRA_ATTSemakan_CutiUmum { get; set; }
        public DbSet<v_HRA_ATTSemakan_BorangA> v_HRA_ATTSemakan_BorangA { get; set; }
        public DbSet<v_HRA_ATTSemakan_BorangC> v_HRA_ATTSemakan_BorangC { get; set; }
        public DbSet<v_HRA_ATTSemakan_BorangTugasan> v_HRA_ATTSemakan_BorangTugasan { get; set; }
        public DbSet<v_HRA_ATTSemakan_CutiSkokraf> v_HRA_ATTSemakan_CutiSkokraf { get; set;}
        public DbSet<v_stafflist> v_stafflist { get; set; }
        public DbSet<tbl_Profile> tbl_Profile { get; set; }

        // Optional: Only needed if you want to configure options manually
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<v_HRA_AttKesalahan>().HasNoKey();
            modelBuilder.Entity<v_HRA_ATTSemakan_CutiUmum>().HasNoKey();

            modelBuilder.Entity<tbl_ATTKesalahan>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id)
                    .UseIdentityColumn(1, 1); // Start at 1, increment by 1
            });
        }

    }
}
