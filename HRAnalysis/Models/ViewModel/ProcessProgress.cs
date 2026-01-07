namespace HRAnalysis.Models.ViewModel
{
    public class ProcessProgress
    {
        public int TotalUsers { get; set; }
        public int CompletedUsers { get; set; }
        public string CurrentUser { get; set; }
        public int TotalRecords { get; set; }
        public bool Completed { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
    }
}
