// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;

namespace Content.Shared._Pirate.Photo;

public sealed class PersistentPhotoAlbumSnapshot
{
    public string OwnerKind { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public string AlbumKey { get; init; } = string.Empty;
    public DateTime SavedAt { get; init; }
    public List<PersistentPhotoData> Photos { get; init; } = new();
}
