// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Photo;

[Serializable, NetSerializable]
public sealed class PhotoCardUiState(byte[]? imageData, string? customName, string? caption) : BoundUserInterfaceState
{
    public byte[]? ImageData = imageData;
    public string? CustomName = customName;
    public string? Caption = caption;
}

[Serializable, NetSerializable]
public enum PhotoCardUiKey : byte
{
    Key
}
