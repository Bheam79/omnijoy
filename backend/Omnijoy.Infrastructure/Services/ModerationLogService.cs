using Microsoft.EntityFrameworkCore;
using Omnijoy.Core.DTOs.Admin;
using Omnijoy.Core.Interfaces;
using Omnijoy.Core.Models;
using Omnijoy.Core.Models.Enums;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

public class ModerationLogService : IModerationLogService
{
    private readonly OmnijoyDbContext _db;

    public ModerationLogService(OmnijoyDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Guid> LogAsync(
        Guid actorId,
        ModerationAction action,
        string targetType,
        string targetId,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(targetType))
            throw new ArgumentException("Target type is required.", nameof(targetType));
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Target id is required.", nameof(targetId));

        var entry = new ModerationLog
        {
            Id         = Guid.NewGuid(),
            ActorId    = actorId,
            Action     = action,
            TargetType = targetType.Trim(),
            TargetId   = targetId.Trim(),
            Notes      = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt  = DateTime.UtcNow,
        };

        _db.ModerationLogs.Add(entry);
        await _db.SaveChangesAsync();
        return entry.Id;
    }

    /// <inheritdoc />
    public async Task<ModerationLogListResult> ListAsync(
        Guid? actorId,
        string? action,
        int page,
        int pageSize)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.ModerationLogs
            .AsNoTracking()
            .Include(m => m.Actor)
            .AsQueryable();

        if (actorId is { } a)
            query = query.Where(m => m.ActorId == a);

        if (!string.IsNullOrWhiteSpace(action) &&
            Enum.TryParse<ModerationAction>(action, ignoreCase: true, out var parsedAction))
        {
            query = query.Where(m => m.Action == parsedAction);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ModerationLogListResult(
            Items:    items.Select(ToDto).ToArray(),
            Page:     page,
            PageSize: pageSize,
            HasMore:  page * pageSize < total
        );
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ModerationLogDto ToDto(ModerationLog m) => new(
        Id:               m.Id,
        ActorId:          m.ActorId,
        ActorDisplayName: m.Actor?.DisplayName ?? string.Empty,
        Action:           m.Action.ToString(),
        TargetType:       m.TargetType,
        TargetId:         m.TargetId,
        Notes:            m.Notes,
        CreatedAt:        m.CreatedAt
    );
}
