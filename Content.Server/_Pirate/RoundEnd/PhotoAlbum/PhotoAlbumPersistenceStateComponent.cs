// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;

[RegisterComponent]
public sealed partial class PhotoAlbumPersistenceStateComponent : Component
{
    public string OwnerKind = string.Empty;
    public string OwnerId = string.Empty;
    public string AlbumKey = string.Empty;
}
