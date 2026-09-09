using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Services;

/// <summary>
/// Erases a user's notification center rows and delivery preferences on account erasure (GDPR-2,
/// ADR-013). Registered as an <see cref="IUserDataContributor"/> so NOTIFY's user-keyed tables are
/// wiped without <c>AccountErasureService</c> knowing about them.
/// </summary>
public sealed class NotificationUserDataContributor(
    IRepository<Notification> notifications,
    IRepository<NotificationPreference> preferences) : IUserDataContributor
{
    public async Task WipeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await notifications.Query().Where(n => n.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await preferences.Query().Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
