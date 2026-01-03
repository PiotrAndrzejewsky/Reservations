namespace Reservations.Models
{
    public class Reservation
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int? SessionId { get; set; }
        public Session? Session { get; set; }
        public int? LaneId { get; set; }
        public Lane? Lane { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SlotStart { get; set; } // New: slot start time for lane reservations (UTC)
    }
}
