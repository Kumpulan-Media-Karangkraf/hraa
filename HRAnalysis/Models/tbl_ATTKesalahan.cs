using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRAnalysis.Models
{
    public class tbl_ATTKesalahan
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string? IdStaff { get; set; }

        public string? Name { get; set; }

        public DateTime Trk { get; set; } // Tarikh kesalahan

        public string? JenisKesalahan { get; set; } // Example: "Datang Lewat", "Ponteng", etc.

        public bool? Exclude { get; set; } // If true, excluded from final output (e.g., due to semakan)

        public int? IdSemakan { get; set; } // Foreign key to tbl_ATTSemakanV1 (nullable)

        public string? Syrt { get; set; } // Syarikat
        public string? Jab { get; set; }  // Jabatan
        public string? Bhgn { get; set; } // Bahagian
        public string? Jaw { get; set; }  // Jawatan
        public int? IdProfile { get; set; }
    }
}
