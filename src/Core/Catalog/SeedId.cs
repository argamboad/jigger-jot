using System.Security.Cryptography;
using System.Text;

namespace JiggerJot.Core.Catalog;

/// <summary>
/// Derives a stable identifier for a seeded catalog row from what the row <b>is</b>, rather than
/// storing one. The same glass, unit, method or category therefore carries the same id on every
/// developer's machine, on staging, and on production — which is what lets a later seed pass reference
/// a row it did not create, with no lookup table and no import order to get wrong.
/// <para>
/// The alternative — committing a literal GUID per row — was rejected because it makes the seed file
/// unreadable and puts nothing in the repository that explains why a given row has a given id.
/// </para>
/// <para>
/// <b>A row's name is its identity.</b> Renaming a seeded row silently changes its id and orphans every
/// reference to it; that is a data migration, not an edit to the seed file. Names are folded to
/// lower case and trimmed before hashing, so "Rocks glass" and "rocks glass " are the same row —
/// capitalisation is presentation, not identity.
/// </para>
/// </summary>
public static class SeedId
{
    /// <summary>
    /// A random constant that scopes every derived id to JiggerJot's seed catalog, so a name as ordinary
    /// as "Coffee" cannot collide with an id derived by anything else that hashes names into GUIDs.
    /// </summary>
    private static readonly byte[] Namespace = new Guid("7d1f6d0e-3a24-4c58-9d5b-2f8e0c4a91b6").ToByteArray();

    /// <summary>
    /// The id for a seeded row. <paramref name="kind"/> separates the vocabularies, so the glass named
    /// "Coupe" and a hypothetical method of the same name never derive the same id.
    /// </summary>
    public static Guid For(string kind, string name)
    {
        var key = Encoding.UTF8.GetBytes($"{kind}:{name.Trim().ToLowerInvariant()}");
        var input = new byte[Namespace.Length + key.Length];
        Namespace.CopyTo(input, 0);
        key.CopyTo(input, Namespace.Length);

        // SHA-256 rather than the SHA-1 of a name-based UUID v5: the digest is not a security boundary
        // here, but shipping SHA-1 at all invites a recurring conversation with every scanner and
        // reviewer that reads this file. Truncating a stronger hash costs nothing and ends it.
        var hash = SHA256.HashData(input);
        var bytes = hash.AsSpan(0, 16).ToArray();

        // Stamp the RFC 9562 version 8 (custom) and variant bits, so what comes out is a well-formed
        // GUID that honestly reports how it was made instead of impersonating a random one.
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        // Guid's constructor reads the first three groups in machine byte order; going through the
        // big-endian overload keeps the id identical on a big-endian machine.
        return new Guid(bytes, bigEndian: true);
    }
}
