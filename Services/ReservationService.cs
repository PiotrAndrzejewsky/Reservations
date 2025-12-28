using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;

namespace Reservations.Services
{
    public class ReservationService : IReservationService
    {
        private readonly ReservationsDbContext _db;

        public ReservationService(ReservationsDbContext db)
        {
            _db = db;
        }

        public async Task<Reservation> CreateReservationAsync(int userId, int? sessionId, int? laneId)
        {
            var reservation = new Reservation
            {
                UserId = userId,
                SessionId = sessionId,
                LaneId = laneId,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();

            return reservation;
        }

        public async Task<bool> CancelReservationAsync(int reservationId)
        {
            var res = await _db.Reservations.FindAsync(reservationId);
            if (res == null) return false;
            _db.Reservations.Remove(res);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<Reservation>> GetUserReservationsAsync(int userId)
        {
            return await _db.Reservations
                .Include(r => r.Session)
                .Include(r => r.Lane)
                .Where(r => r.UserId == userId)
                .ToListAsync();
        }
    }
}
