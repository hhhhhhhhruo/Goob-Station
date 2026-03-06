// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.RoundEnd.PhotoAlbum;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using System.IO;
using System.Numerics;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

public sealed partial class RoundEndSummaryWindow
{
    private readonly Dictionary<int, string> _photoDownloadPaths = new();
    private readonly List<(TextureButton Button, Action<ButtonEventArgs> Handler)> _photoDownloadHandlers = new();
    private readonly List<TextureRect> _photoTextureRects = new();
    private BoxContainer? _photoReportTab;
    private int _nextPhotoDownloadId;

    public void AddOrUpdatePhotoReportTab()
    {
        if (_photoReportTab != null)
            return;

        var photoTab = MakePhotoReportTab();
        if (photoTab is null)
            return;

        _photoReportTab = photoTab;
        _roundEndTabs.AddChild(photoTab);
    }

    private BoxContainer? MakePhotoReportTab()
    {
        var stationAlbumSystem = _entityManager.System<PhotoAlbumSystem>();
        var spriteSystem = _entityManager.System<SpriteSystem>();
        ReleasePhotoResources();
        OnClose += ReleasePhotoResources;

        var stationAlbumTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-photo-album-tab-title")
        };

        var stationAlbumTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-photo-album-tab-title")
        };

        if (stationAlbumSystem.Albums is null || stationAlbumSystem.Albums.Count == 0)

        if (stationAlbumSystem.Albums is null || stationAlbumSystem.Albums.Count == 0)
            return null;

        var stationAlbumContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10),
            HScrollEnabled = false,
        };

        var stationAlbumContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        SpriteSpecifier.Texture downloadIconTexture = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/in.svg.192dpi.png"));

        foreach (var album in stationAlbumSystem.Albums)
        {
            var gridContainer = new GridContainer();

            gridContainer.Columns = 2;
            gridContainer.HorizontalExpand = true;

            foreach (var image in album.Images)
            {
                Texture texture;
                try
                {
                    using var stream = new MemoryStream(image.Key);
                    texture = Texture.LoadFromPNGStream(stream);
                }
                catch
                {
                    continue;
                }

                var imageLabel = new RichTextLabel();

                if (image.Value is not null)
                    imageLabel.SetMessage(image.Value);
                else
                    imageLabel.SetMessage(Loc.GetString("round-end-summary-album-photo-no-name"));

                var imageContainer = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalExpand = true
                };

                TextureRect textureRect = new TextureRect
                {
                    Margin = new Thickness(5, 10, 5, 5)
                };
                _photoTextureRects.Add(textureRect);

                TextureButton downloadButton = new TextureButton
                {
                    HorizontalAlignment = HAlignment.Right,
                    VerticalAlignment = VAlignment.Bottom
                };

                var downloadId = _nextPhotoDownloadId++;
                try
                {
                    var tempPath = Path.Combine(
                        Path.GetTempPath(),
                        $"ss14-round-end-photo-{RoundId}-{downloadId}-{Guid.NewGuid():N}.png");
                    File.WriteAllBytes(tempPath, image.Key);
                    _photoDownloadPaths[downloadId] = tempPath;

                    Action<ButtonEventArgs> onPressed = args => DownloadButton_OnPressed(args, downloadId);
                    downloadButton.OnPressed += onPressed;
                    _photoDownloadHandlers.Add((downloadButton, onPressed));
                }
                catch (Exception ex)
                {
                    downloadButton.Disabled = true;
                    Log.Warning($"Failed to cache round-end photo {downloadId} for download: {ex}");
                }

                downloadButton.Scale = new Vector2(0.5f, 0.5f);
                downloadButton.TextureNormal = spriteSystem.Frame0(downloadIconTexture);

                textureRect.Texture = texture;
                textureRect.AddChild(downloadButton);

                var panel = new PanelContainer
                {
                    StyleClasses = { StyleNano.StyleClassBackgroundBaseDark },
                };

                imageContainer.AddChild(textureRect);
                imageContainer.AddChild(imageLabel);

                panel.AddChild(imageContainer);

                gridContainer.AddChild(panel);
            }

            stationAlbumContainer.AddChild(gridContainer);

            var stationAlbumAuthorHeaderContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                VerticalExpand = true,
                Margin = new Thickness(0, 5, 0, 5)
            };

            var stationAlbumAuthorHeaderPanel = new PanelContainer
            {
                StyleClasses = { StyleNano.StyleClassBackgroundBaseDark },
                SetSize = new Vector2(556, 30),
                HorizontalAlignment = HAlignment.Left
            };

            var stationAlbumAuthorHeaderLabel = new RichTextLabel();

            string authorName = album.AuthorName == null ? Loc.GetString("round-end-summary-album-photo-no-author-name") : album.AuthorName;
            string authorCKey = album.AuthorCkey == null ? Loc.GetString("round-end-summary-album-photo-no-author-ckey") : album.AuthorCkey;

            stationAlbumAuthorHeaderLabel.SetMarkup(Loc.GetString("round-end-summary-album-photo-author", ("authorName", authorName), ("authorCKey", authorCKey)));

            stationAlbumAuthorHeaderPanel.AddChild(stationAlbumAuthorHeaderLabel);
            stationAlbumAuthorHeaderContainer.AddChild(stationAlbumAuthorHeaderPanel);

            stationAlbumContainer.AddChild(stationAlbumAuthorHeaderContainer);
        }

        stationAlbumContainerScrollbox.AddChild(stationAlbumContainer);
        stationAlbumTab.AddChild(stationAlbumContainerScrollbox);

        stationAlbumSystem.ClearImagesData();

        return stationAlbumTab;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            ReleasePhotoResources();

        base.Dispose(disposing);
    }

    private void ReleasePhotoResources()
    {
        OnClose -= ReleasePhotoResources;

        foreach (var (button, handler) in _photoDownloadHandlers)
        {
            button.OnPressed -= handler;
        }
        _photoDownloadHandlers.Clear();

        foreach (var textureRect in _photoTextureRects)
        {
            textureRect.Texture = null;
        }
        _photoTextureRects.Clear();

        foreach (var cachedPath in _photoDownloadPaths.Values)
        {
            try
            {
                if (File.Exists(cachedPath))
                    File.Delete(cachedPath);
            }
            catch (Exception ex)
            {
                Log.Warning($"Failed to delete cached round-end photo file '{cachedPath}': {ex}");
            }
        }
        _photoDownloadPaths.Clear();
    }

    private async void DownloadButton_OnPressed(ButtonEventArgs _, int imageId)
    {
        if (!_photoDownloadPaths.TryGetValue(imageId, out var cachedPath) || !File.Exists(cachedPath))
        {
            Log.Warning($"Round-end photo download cache miss for image id {imageId}.");
            return;
        }

        var file = await _fileDialogManager.SaveFile(new FileDialogFilters(new FileDialogFilters.Group("png")));

        if (!file.HasValue)
            return;

        try
        {
            await using var source = File.OpenRead(cachedPath);
            await source.CopyToAsync(file.Value.fileStream);
        }
        finally
        {
            await file.Value.fileStream.DisposeAsync();
        }
    }
}
