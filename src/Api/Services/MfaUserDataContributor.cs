using Microsoft.EntityFrameworkCore;
using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;

namespace JiggerJot.Api.Services;

/// <summary>
/// Erases a user's MFA state — the encrypted TOTP secret and hashed recovery codes — on account erasure
/// (GDPR-2, ADR-012). Registered as an <see cref="IUserDataContributor"/> so MFA's user-keyed tables are
/// wiped without <c>AccountErasureService</c> knowing about them.
/// </summary>
public sealed class MfaUserDataContributor(
    IRepository<UserMfa> userMfa,
    IRepository<MfaRecoveryCode> recoveryCodes) : IUserDataContributor
{
    public async Task WipeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await recoveryCodes.Query().Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await userMfa.Query().Where(m => m.UserId == userId).ExecuteDeleteAsync(cancellationToken);
    }
}
