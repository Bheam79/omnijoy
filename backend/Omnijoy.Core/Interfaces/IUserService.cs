using Omnijoy.Core.DTOs.Auth;
using Omnijoy.Core.DTOs.Users;

namespace Omnijoy.Core.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Returns a user's public profile. Privacy settings are respected:
    /// fields are omitted when the requester lacks permission to view them.
    /// </summary>
    /// <param name="userId">Profile owner's ID.</param>
    /// <param name="requesterId">Viewing user's ID (null = unauthenticated).</param>
    Task<UserProfileDto> GetUserProfileAsync(Guid userId, Guid? requesterId);

    /// <summary>Updates editable profile fields for the authenticated user.</summary>
    Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);

    /// <summary>Replaces the user's avatar image. Returns updated UserDto.</summary>
    Task<UserDto> UploadAvatarAsync(Guid userId, Stream fileStream, string fileName);

    /// <summary>Replaces the user's cover photo. Returns updated UserDto.</summary>
    Task<UserDto> UploadCoverAsync(Guid userId, Stream fileStream, string fileName);

    /// <summary>Returns the authenticated user's privacy settings.</summary>
    Task<PrivacySettingsDto> GetPrivacySettingsAsync(Guid userId);

    /// <summary>Updates privacy settings. Only provided fields are changed.</summary>
    Task<PrivacySettingsDto> UpdatePrivacySettingsAsync(Guid userId, UpdatePrivacyRequest request);
}
