using Reservations.Models;

namespace Reservations.Services
{
    public interface IReservationService
    {
        Task<Reservation> CreateReservationAsync(int userId, int? sessionId, int? laneId);
        Task<bool> CancelReservationAsync(int reservationId);
        Task<IEnumerable<Reservation>> GetUserReservationsAsync(int userId);
    }
}
