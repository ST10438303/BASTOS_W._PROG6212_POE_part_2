using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs.Events;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers
{
    /// <summary>
    /// Organisers can create, update, and delete events. Both Organisers and
    /// Participants can view events - browsing the event list is how Participants
    /// discover events to enter.
    /// </summary>
    [ApiController]
    [Route("api/events")]
    public class EventsController : ControllerBase
    {
        private readonly RaceDayContext _context;

        public EventsController(RaceDayContext context)
        {
            _context = context;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>Lists all events. Public - no login required, so Participants can browse before registering.</summary>
        /// <response code="200">Returns the list of events.</response>
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IEnumerable<EventResponseDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAll()
        {
            // Materialize the entities first (ToListAsync), then map to DTOs in
            // memory - ToDto cannot be translated to SQL by the EF Core provider.
            var events = await _context.Events
                .Include(e => e.Organiser)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(events.Select(ToDto));
        }

        /// <summary>Retrieves a single event by id, including its categories.</summary>
        /// <response code="200">Returns the event.</response>
        /// <response code="404">Event not found.</response>
        [HttpGet("{id}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id)
        {
            var ev = await _context.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound(new { message = "Event not found." });

            return Ok(ToDto(ev));
        }

        /// <summary>Creates a new event. Organiser role only.</summary>
        /// <response code="201">Event created.</response>
        /// <response code="400">Validation failed (e.g. invalid EventType).</response>
        /// <response code="401">Not authenticated.</response>
        /// <response code="403">Logged in user is not an Organiser.</response>
        [HttpPost]
        [Authorize(Roles = RoleNames.Organiser)]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Create([FromBody] EventCreateDto dto)
        {
            if (!EventTypes.IsValid(dto.EventType))
            {
                return BadRequest(new { message = "EventType must be 'Run', 'Walk', or 'Cycle'." });
            }

            var ev = new Event
            {
                OrganiserId = CurrentUserId,
                EventName = dto.EventName,
                Description = dto.Description,
                EventDate = dto.EventDate,
                Location = dto.Location,
                RouteInfo = dto.RouteInfo,
                Distance = dto.Distance,
                EventType = dto.EventType,
                DateCreated = DateTime.UtcNow
            };

            _context.Events.Add(ev);
            await _context.SaveChangesAsync();
            await _context.Entry(ev).Reference(e => e.Organiser).LoadAsync();

            return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ToDto(ev));
        }

        /// <summary>Updates an event owned by the logged-in Organiser.</summary>
        /// <response code="200">Event updated.</response>
        /// <response code="400">Validation failed.</response>
        /// <response code="403">Not the owner of this event.</response>
        /// <response code="404">Event not found.</response>
        [HttpPut("{id}")]
        [Authorize(Roles = RoleNames.Organiser)]
        [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Update(int id, [FromBody] EventUpdateDto dto)
        {
            var ev = await _context.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
            if (ev == null) return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != CurrentUserId)
            {
                return Forbid();
            }

            if (!EventTypes.IsValid(dto.EventType))
            {
                return BadRequest(new { message = "EventType must be 'Run', 'Walk', or 'Cycle'." });
            }

            ev.EventName = dto.EventName;
            ev.Description = dto.Description;
            ev.EventDate = dto.EventDate;
            ev.Location = dto.Location;
            ev.RouteInfo = dto.RouteInfo;
            ev.Distance = dto.Distance;
            ev.EventType = dto.EventType;

            await _context.SaveChangesAsync();
            return Ok(ToDto(ev));
        }

        /// <summary>Deletes an event owned by the logged-in Organiser.</summary>
        /// <response code="200">Event deleted.</response>
        /// <response code="403">Not the owner of this event.</response>
        /// <response code="404">Event not found.</response>
        /// <response code="409">Event has enrolments and cannot be deleted.</response>
        [HttpDelete("{id}")]
        [Authorize(Roles = RoleNames.Organiser)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Delete(int id)
        {
            var ev = await _context.Events.FindAsync(id);
            if (ev == null) return NotFound(new { message = "Event not found." });

            if (ev.OrganiserId != CurrentUserId)
            {
                return Forbid();
            }

            // Enrolments restrict deletion of their category (and therefore the event),
            // so refuse politely instead of letting the database throw.
            var hasEnrolments = await _context.Enrolments.AnyAsync(e => e.Category!.EventId == id);
            if (hasEnrolments)
            {
                return Conflict(new { message = "This event has enrolments and cannot be deleted." });
            }

            _context.Events.Remove(ev);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Event deleted." });
        }

        private static EventResponseDto ToDto(Event e) => new()
        {
            EventId = e.EventId,
            OrganiserId = e.OrganiserId,
            OrganiserName = e.Organiser?.FullName ?? string.Empty,
            EventName = e.EventName,
            Description = e.Description,
            EventDate = e.EventDate,
            Location = e.Location,
            RouteInfo = e.RouteInfo,
            Distance = e.Distance,
            EventType = e.EventType,
            DateCreated = e.DateCreated
        };
    }
}
