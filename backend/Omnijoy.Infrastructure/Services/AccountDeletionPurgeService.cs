using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnijoy.Core.Interfaces;
using Omnijoy.Infrastructure.Data;

namespace Omnijoy.Infrastructure.Services;

/// <summary>
/// Background job that permanently purges accounts whose deletion grace period has elapsed.
/// Runs on a configurable interval (default: every 24h).
/// Grace period is configured via <c>Account:DeletionGraceDays</c> (default: 30).
///
/// Trade-offs:
///   • Runs in every instance — multi-node deployments will execute the purge
///     batch N times per interval. The work is idempotent (each user is removed
///     in its own transaction; once gone, the row is no longer matched on the
///     next tick), so duplicate effort is harmless. A failed instance simply
///     means the next instance picks the work up on its tick.
///   • Per-user failure (FK violation, storage outage) is logged and the batch
///     continues with the next user — one bad row never blocks the queue.
///   • Media-file deletion is best-effort: a storage failure leaves the file
///     orphaned in the bucket but DB rows are already purged. Re-running the
///     job will not retry the file deletion (the User row is gone).
/// </summary>
public class AccountDeletionPurgeService : BackgroundService
{
    public static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(24);

    private readonly IServiceProvider _services;
    private readonly ILogger<AccountDeletionPurgeService> _log;

    public AccountDeletionPurgeService(IServiceProvider services, ILogger<AccountDeletionPurgeService> log)
    {
        _services = services;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay: give the app time to start before hitting the DB.
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            await PurgeOnceAsync(stoppingToken);

            try { await Task.Delay(PurgeInterval, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    /// <summary>
    /// Single purge cycle. Exposed (internal) so tests can drive one batch
    /// without spinning the long-running <c>ExecuteAsync</c> loop.
    /// </summary>
    internal async Task PurgeOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db      = scope.ServiceProvider.GetRequiredService<OmnijoyDbContext>();
            var storage = scope.ServiceProvider.GetRequiredService<IMediaStorageService>();
            var config  = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var graceDays = config.GetValue<int>("Account:DeletionGraceDays", 30);
            var cutoff    = DateTime.UtcNow.AddDays(-graceDays);

            var users = await db.Users
                .Where(u => u.DeletionScheduledAt != null && u.DeletionScheduledAt <= cutoff)
                .ToListAsync(ct);

            foreach (var user in users)
            {
                try
                {
                    await PurgeUserAsync(db, storage, user, ct);
                    _log.LogInformation(
                        "AccountDeletionPurge: purged user {UserId} (DeletionScheduledAt={ScheduledAt})",
                        user.Id, user.DeletionScheduledAt);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "AccountDeletionPurge: failed to purge user {UserId}", user.Id);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "AccountDeletionPurge: batch tick failed");
        }
    }

    private async Task PurgeUserAsync(
        OmnijoyDbContext db,
        IMediaStorageService storage,
        Omnijoy.Core.Models.User user,
        CancellationToken ct)
    {
        // ── Collect media URLs for deferred deletion from object store ──────────
        // Done first because the rows that hold these URLs are about to go away.
        var postMediaUrls = await db.PostMedia
            .Where(pm => pm.Post.AuthorUserId == user.Id)
            .Select(pm => new { pm.Url, pm.ThumbnailUrl })
            .ToListAsync(ct);

        var msgMediaUrls = await db.MessageMedia
            .Where(mm => mm.Message.SenderUserId == user.Id)
            .Select(mm => mm.Url)
            .ToListAsync(ct);

        var eventCoverUrls = await db.Events
            .Where(e => e.CreatorUserId == user.Id && e.CoverImageUrl != null)
            .Select(e => e.CoverImageUrl!)
            .ToListAsync(ct);

        // Materialize first — SelectMany over a `new[]{…}` array isn't
        // translatable by the EF query provider.
        var pageImageUrlPairs = await db.CompanyPages
            .Where(cp => cp.CreatedByUserId == user.Id)
            .Select(cp => new { cp.LogoUrl, cp.CoverUrl })
            .ToListAsync(ct);
        var pageImageUrls = pageImageUrlPairs
            .SelectMany(p => new[] { p.LogoUrl, p.CoverUrl })
            .Where(u => u != null)
            .Select(u => u!)
            .ToList();

        // ── Delete rows that have Restrict FK constraints on User ───────────────
        // Order matters: children of posts/events/companyPages must go first so
        // the parent rows can be deleted without FK violations.

        // SharedPosts authored by this user
        db.SharedPosts.RemoveRange(
            await db.SharedPosts.Where(sp => sp.SharerId == user.Id).ToListAsync(ct));

        // Comments authored by this user on any post
        db.Comments.RemoveRange(
            await db.Comments.Where(c => c.AuthorId == user.Id).ToListAsync(ct));

        // Messages sent by this user (MessageMedia cascades)
        db.Messages.RemoveRange(
            await db.Messages.Where(m => m.SenderUserId == user.Id).ToListAsync(ct));

        // Posts (cascades: PostMedia rows, PostReactions, Comments on these posts,
        //        SharedPosts that reference these posts as OriginalPostId)
        db.Posts.RemoveRange(
            await db.Posts.Where(p => p.AuthorUserId == user.Id).ToListAsync(ct));

        // Friend rows (both directions — both FKs are Restrict)
        db.Friends.RemoveRange(
            await db.Friends
                .Where(f => f.RequesterId == user.Id || f.AddresseeId == user.Id)
                .ToListAsync(ct));

        // FamilyRelation rows (both directions — both FKs are Restrict)
        db.FamilyRelations.RemoveRange(
            await db.FamilyRelations
                .Where(fr => fr.UserId == user.Id || fr.RelatedUserId == user.Id)
                .ToListAsync(ct));

        // LiveStreams hosted by user
        db.LiveStreams.RemoveRange(
            await db.LiveStreams.Where(ls => ls.HostUserId == user.Id).ToListAsync(ct));

        // Reports filed by user
        db.Reports.RemoveRange(
            await db.Reports.Where(r => r.ReporterId == user.Id).ToListAsync(ct));

        // ModerationLogs authored by user
        db.ModerationLogs.RemoveRange(
            await db.ModerationLogs.Where(ml => ml.ActorId == user.Id).ToListAsync(ct));

        // Events created by user (cascades: EventAttendees, event-wall Posts)
        db.Events.RemoveRange(
            await db.Events.Where(e => e.CreatorUserId == user.Id).ToListAsync(ct));

        // CompanyPages created by user (cascades: CompanyPageAdmins, CompanyPageFollows)
        db.CompanyPages.RemoveRange(
            await db.CompanyPages.Where(cp => cp.CreatedByUserId == user.Id).ToListAsync(ct));

        await db.SaveChangesAsync(ct);

        // ── Delete the User row ─────────────────────────────────────────────────
        // Remaining cascades fire here: Notifications, RefreshTokens, AuthProviders,
        // EventAttendees, CompanyPageAdminships, CompanyPageFollows,
        // ConversationParticipants, PostReactions, FriendInvites,
        // UserPrivacySettings, NotificationPreferences.
        db.Users.Remove(user);
        await db.SaveChangesAsync(ct);

        // ── Clean up empty conversations ────────────────────────────────────────
        // After ConversationParticipant rows cascade-deleted, orphaned conversations
        // (no remaining participants AND no remaining messages) are pruned.
        var orphanIds = await db.Conversations
            .Where(c => !db.ConversationParticipants.Any(cp => cp.ConversationId == c.Id)
                     && !db.Messages.Any(m => m.ConversationId == c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (orphanIds.Count > 0)
        {
            db.Conversations.RemoveRange(
                await db.Conversations.Where(c => orphanIds.Contains(c.Id)).ToListAsync(ct));
            await db.SaveChangesAsync(ct);
        }

        // ── Delete files from object storage ────────────────────────────────────
        var allUrls = postMediaUrls.SelectMany(pm => new[] { pm.Url, pm.ThumbnailUrl })
            .Concat(msgMediaUrls)
            .Concat(eventCoverUrls)
            .Concat(pageImageUrls)
            .Append(user.AvatarUrl)
            .Append(user.CoverUrl)
            .Where(u => !string.IsNullOrEmpty(u))
            .Select(u => u!)
            .Distinct()
            .ToList();

        foreach (var url in allUrls)
        {
            try { await storage.DeleteAsync(url); }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "AccountDeletionPurge: failed to delete media file {Url}", url);
            }
        }
    }
}
