namespace Reservations.Models
{
    public class Lane
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Capacity { get; set; }
    }
}
