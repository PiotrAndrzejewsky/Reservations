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
            // If laneId provided, we expect SlotStart to be encoded in sessionId as ticks?
            // New approach: sessionId == null and laneId != null must be accompanied by SlotStart set via an overload.
            throw new NotSupportedException("Use CreateLaneReservationAsync or CreateSessionReservationAsync for explicit behavior.");
        }

        public async Task<Reservation> CreateLaneReservationAsync(int userId, int laneId, DateTime slotStartUtc)
        {
            var lane = await _db.Lanes.FindAsync(laneId);
            if (lane == null) throw new InvalidOperationException("Lane not found.");

            // Prevent double reservation by same user for same lane+slot
            var existing = await _db.Reservations.FirstOrDefaultAsync(r => r.LaneId == laneId && r.SlotStart == slotStartUtc && r.UserId == userId);
            if (existing != null) throw new InvalidOperationException("You already have a reservation for this lane at that time.");

            // Count reservations for this lane+slot
            var count = await _db.Reservations.CountAsync(r => r.LaneId == laneId && r.SlotStart == slotStartUtc);
            if (count >= lane.Capacity) throw new InvalidOperationException("No available slots for this lane at the selected time.");

            var res = new Reservation
            {
                UserId = userId,
                LaneId = laneId,
                SlotStart = slotStartUtc,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reservations.Add(res);
            await _db.SaveChangesAsync();
            return res;
        }

        public async Task<Reservation> CreateSessionReservationAsync(int userId, int sessionId)
        {
            var session = await _db.Sessions.FindAsync(sessionId);
            if (session == null) throw new InvalidOperationException("Session not found.");

            var existing = await _db.Reservations.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.UserId == userId && r.LaneId == null);
            if (existing != null) throw new InvalidOperationException("You already have a reservation for this session.");

            var reservedCount = await _db.Reservations.CountAsync(r => r.SessionId == sessionId && r.LaneId == null);
            if (reservedCount >= session.AvailableSlots) throw new InvalidOperationException("No available slots for this session.");

            var reservation = new Reservation
            {
                UserId = userId,
                SessionId = sessionId,
                LaneId = null,
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
