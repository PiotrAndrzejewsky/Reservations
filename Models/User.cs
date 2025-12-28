namespace Reservations.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;

        // Simple password hash (SHA256) stored as hex string
        public string PasswordHash { get; set; } = null!;
    }
}
