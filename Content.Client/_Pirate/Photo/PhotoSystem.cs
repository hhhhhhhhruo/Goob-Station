// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.Photo.UI;
using Content.Shared._Pirate.Photo;

namespace Content.Client._Pirate.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    public Dictionary<PhotoCameraComponent, PhotoCameraBoundUserInterface> ActiveCameras = new();

    public override void Update(float frameTime)

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (component, window) in ActiveCameras)
        {
            window.UpdateControl(component, frameTime);
        }
    }

    public void OpenCameraUi(PhotoCameraComponent component, PhotoCameraBoundUserInterface window)
    {
        if (ActiveCameras.ContainsKey(component))
            return;

        ActiveCameras.Add(component, window);
    }

    public void CloseCameraUi(PhotoCameraComponent component)
    {
        if (!ActiveCameras.ContainsKey(component))
            return;

        ActiveCameras.Remove(component);
    }
}
