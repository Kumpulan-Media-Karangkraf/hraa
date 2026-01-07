namespace HRAnalysis.Models.ViewModel
{
    public class SemakResultViewModel
    {
        public List<tbl_ATTKesalahan> KesalahanRecords { get; set; } = new List<tbl_ATTKesalahan>();
        public List<MatchedRecordViewModel> MatchedRecords { get; set; } = new List<MatchedRecordViewModel>();
        public int TotalMatched { get; set; }
        public int TotalProcessed { get; set; }
        public bool HasMatches { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
