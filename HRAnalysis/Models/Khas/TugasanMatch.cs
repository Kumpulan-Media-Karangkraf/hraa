namespace HRAnalysis.Models.Khas
{
    public class TugasanMatch
    {
        public v_HRA_ATTSemakan_BorangTugasan Tugasan { get; set; }
        public List<tbl_ATTKesalahan> MatchedKesalahan { get; set; }
    }
}
