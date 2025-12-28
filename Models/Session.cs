namespace Reservations.Models
{
    public class Session
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int AvailableSlots { get; set; }
    }
}
