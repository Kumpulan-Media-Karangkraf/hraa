namespace HRAnalysis.Models.ViewModel
{
    public class MatchedRecordViewModel
    {
        public tbl_ATTKesalahan KesalahanRecord { get; set; }
        public v_HRA_ATTSemakan_BorangA BorangARecord { get; set; }
        public bool IsExactMatch { get; set; }
        public string MatchType { get; set; } // "Exact", "Partial", "Staff+Date"
    }
}
