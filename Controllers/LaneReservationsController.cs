using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reservations.Data;
using Reservations.Models;

namespace Reservations.Controllers
{
    public class LaneReservationsController : Controller
    {
        private readonly ReservationsDbContext _db;
        private static readonly TimeSpan SlotLength = TimeSpan.FromMinutes(30);

        public LaneReservationsController(ReservationsDbContext db)
        {
            _db = db;
        }

        public class SlotRow
        {
            public Session Session { get; set; } = null!;
            public DateTime StartLocal => Session.Start.ToLocalTime();
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

        private async Task EnsureDailySlots(DateTime date)
        {
            var (start, end) = GetDailyRange(date.DayOfWeek);
            var localStart = date.Date.Add(start);
            var localEnd = date.Date.Add(end);

            var existing = await _db.Sessions
                .Where(s => s.Start >= DateTime.SpecifyKind(localStart, DateTimeKind.Local).ToUniversalTime() &&
                            s.End <= DateTime.SpecifyKind(localEnd, DateTimeKind.Local).ToUniversalTime())
                .ToDictionaryAsync(s => s.Start, s => s);

            var toAdd = new List<Session>();
            for (var t = localStart; t < localEnd; t = t.Add(SlotLength))
            {
                var startUtc = DateTime.SpecifyKind(t, DateTimeKind.Local).ToUniversalTime();
                if (!existing.ContainsKey(startUtc))
                {
                    toAdd.Add(new Session
                    {
                        Title = $"Slot {t:HH:mm}",
                        Start = startUtc,
                        End = DateTime.SpecifyKind(t.Add(SlotLength), DateTimeKind.Local).ToUniversalTime(),
                        AvailableSlots = 1000
                    });
                }
            }

            if (toAdd.Count > 0)
            {
                _db.Sessions.AddRange(toAdd);
                await _db.SaveChangesAsync();
            }
        }

        private async Task<IndexViewModel> BuildViewModel(DateTime date)
        {
            await EnsureLanesAsync();
            await EnsureDailySlots(date);

            var (start, end) = GetDailyRange(date.DayOfWeek);
            var rangeStartUtc = DateTime.SpecifyKind(date.Date.Add(start), DateTimeKind.Local).ToUniversalTime();
            var rangeEndUtc = DateTime.SpecifyKind(date.Date.Add(end), DateTimeKind.Local).ToUniversalTime();

            var sessions = await _db.Sessions
                .Where(s => s.Start >= rangeStartUtc && s.End <= rangeEndUtc)
                .OrderBy(s => s.Start)
                .ToListAsync();

            var lanes = await _db.Lanes.OrderBy(l => l.Id).Take(6).ToListAsync();
            var sessionIds = sessions.Select(s => s.Id).ToList();

            var reservations = await _db.Reservations
                .Where(r => r.SessionId != null && r.LaneId != null && sessionIds.Contains(r.SessionId.Value))
                .ToListAsync();

            var laneCounts = reservations
                .GroupBy(r => (r.SessionId!.Value, r.LaneId!.Value))
                .ToDictionary(g => g.Key, g => g.Count());

            var userId = HttpContext.Session.GetInt32("UserId");

            var slots = sessions.Select(s => new SlotRow
            {
                Session = s,
                LaneCounts = lanes.ToDictionary(l => l.Id, l => laneCounts.TryGetValue((s.Id, l.Id), out var c) ? c : 0)
            }).ToList();

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

            await EnsureDailySlots(date);

            var startLocal = date.Date.Add(startTime);
            var endLocal = date.Date.Add(endTime);
            var startUtc = DateTime.SpecifyKind(startLocal, DateTimeKind.Local).ToUniversalTime();
            var endUtc = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();

            var sessions = await _db.Sessions
                .Where(s => s.Start >= startUtc && s.End <= endUtc)
                .OrderBy(s => s.Start)
                .ToListAsync();

            if (sessions.Count == 0)
            {
                TempData["Error"] = "Brak slotów w wybranym zakresie.";
                return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
            }

            foreach (var s in sessions)
            {
                var count = await _db.Reservations.CountAsync(r => r.SessionId == s.Id && r.LaneId == laneId);
                if (count >= lane.Capacity)
                {
                    TempData["Error"] = $"Tor zajêty o {s.Start.ToLocalTime():HH:mm}.";
                    return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
                }

                var already = await _db.Reservations.FirstOrDefaultAsync(r => r.SessionId == s.Id && r.LaneId == laneId && r.UserId == userId.Value);
                if (already != null)
                {
                    TempData["Info"] = $"Masz ju¿ rezerwacjê na {s.Start.ToLocalTime():HH:mm}.";
                    return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
                }
            }

            foreach (var s in sessions)
            {
                _db.Reservations.Add(new Reservation
                {
                    UserId = userId.Value,
                    SessionId = s.Id,
                    LaneId = laneId,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();
            TempData["Info"] = "Zarezerwowano wybrany zakres.";
            return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnreserveLane(int sessionId, int laneId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "LaneReservations") });
            }

            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.SessionId == sessionId && r.LaneId == laneId && r.UserId == userId.Value);
            if (reservation == null)
            {
                TempData["Info"] = "Brak Twojej rezerwacji w tym slocie.";
                return RedirectToAction(nameof(Index));
            }

            var session = await _db.Sessions.FindAsync(sessionId);
            var dateParam = session?.Start.ToLocalTime().ToString("yyyy-MM-dd");

            _db.Reservations.Remove(reservation);
            await _db.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { date = dateParam });
        }
    }
}
