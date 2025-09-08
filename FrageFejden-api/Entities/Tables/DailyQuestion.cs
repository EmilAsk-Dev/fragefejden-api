namespace FrageFejden_api.Entities.Tables
{
    public class DailyQuestion
    {
        public int Id { get; set; }
        public string Category { get; set; } = "";
        public string QuestionText { get; set; } = "";
        public List<string> Alternativ { get; set; } = new();
        public string RattSvar { get; set; } = "";
    }
}
