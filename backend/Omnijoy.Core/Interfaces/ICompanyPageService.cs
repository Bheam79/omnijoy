using Omnijoy.Core.DTOs.CompanyPages;

namespace Omnijoy.Core.Interfaces;

/// <summary>
/// Business logic for company/organization pages: CRUD, admin management, follows.
/// </summary>
public interface ICompanyPageService
{
    /// <summary>Creates a new company page. Creator is auto-assigned as Owner.</summary>
    Task<CompanyPageDto> CreatePageAsync(
        Guid creatorId,
        CreateCompanyPageRequest request,
        PageImageUploadItem? logo,
        PageImageUploadItem? cover);

    /// <summary>Returns a paginated list of company pages visible to the requester.</summary>
    Task<CompanyPagesPageResult> GetPagesAsync(
        Guid requesterId,
        bool mineOnly,
        int page,
        int pageSize);

    /// <summary>Returns a single company page with follow/role status for the requester.</summary>
    Task<CompanyPageDto> GetPageAsync(Guid pageId, Guid? requesterId);

    /// <summary>Updates mutable page fields. Requester must be Owner or Admin.</summary>
    Task<CompanyPageDto> UpdatePageAsync(
        Guid pageId,
        Guid requesterId,
        UpdateCompanyPageRequest request,
        PageImageUploadItem? logo,
        PageImageUploadItem? cover);

    /// <summary>Returns all admins for a company page.</summary>
    Task<AdminsResult> GetAdminsAsync(Guid pageId, Guid? requesterId);

    /// <summary>
    /// Adds or updates a user's admin role on the page.
    /// Requester must be Owner (to set any role) or Admin (to set Editor only).
    /// </summary>
    Task<AdminsResult> AddOrUpdateAdminAsync(Guid pageId, Guid requesterId, AddAdminRequest request);

    /// <summary>
    /// Removes an admin from the page.
    /// Owner can remove anyone. Admin can remove Editors only.
    /// Cannot remove the last Owner.
    /// </summary>
    Task<AdminsResult> RemoveAdminAsync(Guid pageId, Guid requesterId, Guid targetUserId);

    /// <summary>Follows a company page.</summary>
    Task<CompanyPageDto> FollowAsync(Guid pageId, Guid userId);

    /// <summary>Unfollows a company page.</summary>
    Task<CompanyPageDto> UnfollowAsync(Guid pageId, Guid userId);

    /// <summary>Returns company pages where the user has an admin role (for "Posting as" selector).</summary>
    Task<CompanyPageDto[]> GetMyAdminPagesAsync(Guid userId);
}
