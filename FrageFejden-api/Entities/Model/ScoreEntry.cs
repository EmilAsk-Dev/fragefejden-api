public class ScoreEntry
{
    //Primary Key
    public Guid Id { get; set; } 
    //Foreign Key till user
    public Guid UserId { get; set; } 
    public Guid SubjectId { get; set; }
    public int exp { get; set; }
    public DateTime CreatedAt { get; set; }
}