using JiggerJot.Core.Abstractions;
using JiggerJot.Core.Entities;
using JiggerJot.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JiggerJot.Api.Features.UnitPreference;

/// <summary>A measuring preference is the user's own — erased with the account (GDPR-2), never with a household.</summary>
public sealed class UnitPreferenceUserDataContributor(IRepository<UserUnitPreference> preferences) : IUserDataContributor
{
    public Task WipeAsync(Guid userId, CancellationToken cancellationToken = default) =>
        preferences.Query().Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
