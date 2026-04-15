using System;

namespace SLICE_Website.Models
{
    public class CustomerSuggestion
    {
        public int SuggestionID { get; set; }
        public string SuggestionType { get; set; }
        public string Description { get; set; }
        public int SubmittedBy { get; set; }
        public string SubmitterName { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string Status { get; set; }
        public string OwnerNotes { get; set; }
    }
}