using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;
using Reservations.Services;

namespace Reservations.Controllers
{
    public class LaneReservationsController : Controller
    {
        private readonly ReservationsDbContext _db;
        private readonly IReservationService _reservationService;
        private static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

        public LaneReservationsController(ReservationsDbContext db, IReservationService reservationService)
        {
            _db = db;
            _reservationService = reservationService;
        }

        public class SlotRow
        {
            public DateTime SlotStartUtc { get; set; }
            public DateTime StartLocal => SlotStartUtc.ToLocalTime();
            public Dictionary<int, int> LaneCounts { get; set; } = new();
        }

        public class IndexViewModel
        {
            public DateTime Date { get; set; }
            public List<Lane> Lanes { get; set; } = new();
            public List<SlotRow> Slots { get; set; } = new();
            public int? UserId { get; set; }
        }

        private (TimeSpan start, TimeSpan end) GetDailyRange(DayOfWeek day)
        {
            return day is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? (TimeSpan.FromHours(7), TimeSpan.FromHours(21))
                : (TimeSpan.FromHours(6), TimeSpan.FromHours(22));
        }

        private async Task EnsureLanesAsync()
        {
            var lanes = await _db.Lanes.OrderBy(l => l.Id).ToListAsync();
            if (lanes.Count >= 6) return;

            var existingIds = lanes.Select(l => l.Id).ToHashSet();
            var toAdd = new List<Lane>();
            for (int i = 1; i <= 6; i++)
            {
                if (!existingIds.Contains(i))
                {
                    toAdd.Add(new Lane { Id = i, Name = $"Tor {i}", Capacity = 1 });
                }
            }

            if (toAdd.Count > 0)
            {
                _db.Lanes.AddRange(toAdd);
                await _db.SaveChangesAsync();
            }
        }

        private async Task<IndexViewModel> BuildViewModel(DateTime date)
        {
            await EnsureLanesAsync();

            var (start, end) = GetDailyRange(date.DayOfWeek);
            var localStart = date.Date.Add(start);
            var localEnd = date.Date.Add(end);

            var rangeStartUtc = DateTime.SpecifyKind(localStart, DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(localEnd, DateTimeKind.Local).ToUniversalTime();

            var lanes = await _db.Lanes.OrderBy(l => l.Id).Take(6).ToListAsync();

            // Load reservations for lanes that have SlotStart in the range
            var reservations = await _db.Reservations
                .Where(r => r.LaneId != null && r.SlotStart != null && r.SlotStart >= rangeStartUtc && r.SlotStart < rangeEndUtc)
                .ToListAsync();

            // Build slots in memory
            var slots = new List<SlotRow>();
            for (var t = localStart; t < localEnd; t = t.Add(SlotLength))
            {
                var slotStartUtc = DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime();
                var row = new SlotRow
                {
                    SlotStartUtc = slotStartUtc,
                    LaneCounts = lanes.ToDictionary(l => l.Id, l => 0)
                };

                slots.Add(row);
            }

            // Populate counts
            var resBySlotAndLane = reservations
                .GroupBy(r => (r.SlotStart!.Value, r.LaneId!.Value))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var slot in slots)
            {
                foreach (var lane in lanes)
                {
                    if (resBySlotAndLane.TryGetValue((slot.SlotStartUtc, lane.Id), out var c))
                    {
                        slot.LaneCounts[lane.Id] = c;
                    }
                }
            }

            var userId = HttpContext.Session.GetInt32("UserId");

            return new IndexViewModel
            {
                Date = date.Date,
                Lanes = lanes,
                Slots = slots,
                UserId = userId
            };
        }

        [HttpGet]
        public async Task<IActionResult> Index(DateTime? date)
        {
            var targetDate = date?.Date ?? DateTime.Today;
            var vm = await BuildViewModel(targetDate);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangeDate(DateTime date)
        {
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveRange(DateTime date, int laneId, TimeSpan startTime, TimeSpan endTime)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "LaneReservations") });
            }

            if (startTime < TimeSpan.Zero || endTime <= startTime || (endTime - startTime) < SlotLength)
            {
                TempData["Error"] = "Nieprawid³owy zakres czasu.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            bool stepValid(TimeSpan t) => t.TotalMinutes % 30 == 0;
            if (!stepValid(startTime) || !stepValid(endTime))
            {
                TempData["Error"] = "Czas nale¿y podawaæ w krokach co 30 minut.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            var lane = await _db.Lanes.FindAsync(laneId);
            if (lane == null)
            {
                TempData["Error"] = "Wybrany tor nie istnieje.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            var (dayStart, dayEnd) = GetDailyRange(date.DayOfWeek);
            if (startTime < dayStart || endTime > dayEnd)
            {
                TempData["Error"] = "Zakres godziny poza dostêpnoœci¹ obiektu.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            var startLocal = date.Date.Add(startTime);
            var endLocal = date.Date.Add(endTime);

            var slots = new List<DateTime>();
            for (var t = startLocal; t < endLocal; t = t.Add(SlotLength))
            {
                slots.Add(DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime());
            }

            // Check availability using SlotStart on reservations
            foreach (var slotUtc in slots)
            {
                var count = await _db.Reservations.CountAsync(r => r.LaneId == laneId && r.SlotStart == slotUtc);
                if (count >= lane.Capacity)
                {
                    TempData["Error"] = $"Tor zajêty o {slotUtc.ToLocalTime():HH:mm}.";
                    return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
                }

                var already = await _db.Reservations.FirstOrDefaultAsync(r => r.LaneId == laneId && r.SlotStart == slotUtc && r.UserId == userId.Value);
                if (already != null)
                {
                    TempData["Info"] = $"Masz ju¿ rezerwacjê na {slotUtc.ToLocalTime():HH:mm}.";
                    return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
                }
            }

            // Create reservations per slot using reservation service
            foreach (var slotUtc in slots)
            {
                await _reservationService.CreateLaneReservationAsync(userId.Value, laneId, slotUtc);
            }

            TempData["Info"] = "Zarezerwowano wybrany zakres.";
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnreserveLane(DateTime slotStart, int laneId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "LaneReservations") });
            }

            var slotUtc = DateTime.SpecifyKind(slotStart, DateTimeKind.Local).ToUniversalTime();
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.SlotStart == slotUtc && r.LaneId == laneId && r.UserId == userId.Value);
            if (reservation == null)
            {
                TempData["Info"] = "Brak Twojej rezerwacji w tym slocie.";
                return RedirectToAction(nameof(Index));
            }

            await _reservationService.CancelReservationAsync(reservation.Id);
            return RedirectToAction(nameof(Index), new { date = slotUtc.ToLocalTime().ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveSlot(DateTime slotStart, int laneId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "LaneReservations") });
            }

            var lane = await _db.Lanes.FindAsync(laneId);
            if (lane == null)
            {
                TempData["Error"] = "Wybrany tor nie istnieje.";
                return RedirectToAction(nameof(Index));
            }

            var slotUtc = DateTime.SpecifyKind(slotStart, DateTimeKind.Local).ToUniversalTime();

            // Check capacity
            var count = await _db.Reservations.CountAsync(r => r.LaneId == laneId && r.SlotStart == slotUtc);
            if (count >= lane.Capacity)
            {
                TempData["Error"] = "Tor jest ju¿ zajêty w tym slocie.";
                return RedirectToAction(nameof(Index), new { date = slotUtc.ToLocalTime().ToString("yyyy-MM-dd") });
            }

            // Check existing for user
            var already = await _db.Reservations.FirstOrDefaultAsync(r => r.LaneId == laneId && r.SlotStart == slotUtc && r.UserId == userId.Value);
            if (already != null)
            {
                TempData["Info"] = "Masz ju¿ rezerwacjê w tym slocie.";
                return RedirectToAction(nameof(Index), new { date = slotUtc.ToLocalTime().ToString("yyyy-MM-dd") });
            }

            await _reservationService.CreateLaneReservationAsync(userId.Value, laneId, slotUtc);
            TempData["Info"] = "Zarezerwowano tor.";
            return RedirectToAction(nameof(Index), new { date = slotUtc.ToLocalTime().ToString("yyyy-MM-dd") });
        }
    }
}
