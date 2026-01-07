using System.ComponentModel.DataAnnotations;

namespace HRAnalysis.Models
{
    public class v_stafflist
    {
        [Key]
        public string NoPekerja { get; set; }
        public string Nama { get; set; }
        public string Syrt { get; set; }
        public string Jab { get; set; }
        public string Bhgn { get; set; }
        public string username { get; set; }
        public string Kod_Penyelia { get; set; }
        public string HPTelNumber { get; set; }

    }
}
