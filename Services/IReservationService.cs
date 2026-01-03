using Reservations.Models;

namespace Reservations.Services
{
    public interface IReservationService
    {
        Task<Reservation> CreateLaneReservationAsync(int userId, int laneId, DateTime slotStartUtc);
        Task<Reservation> CreateSessionReservationAsync(int userId, int sessionId);
        Task<bool> CancelReservationAsync(int reservationId);
        Task<IEnumerable<Reservation>> GetUserReservationsAsync(int userId);
    }
}
