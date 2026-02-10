using Content.Client.UserInterface.Controls;
using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Content.Goobstation.Common.MartialArts;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using System.Numerics;

namespace Content.Pirate.Client._JustDecor.MartialArts;

public sealed class ComboHelperWidget : UIWidget
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;

    private SpriteSystem _spriteSystem = default!;
    private readonly BoxContainer _container;

    public ComboHelperWidget()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entManager.System<SpriteSystem>();

        LayoutContainer.SetAnchorAndMarginPreset(this, LayoutContainer.LayoutPreset.CenterBottom, margin: 5);

        // Встановлюємо віджет на весь екран, але він ігнорує миші
        MouseFilter = MouseFilterMode.Ignore;

        // Контейнер для рядків комбо
        _container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0), // Відступ від нижнього правого кута
            SeparationOverride = 5,
            MouseFilter = MouseFilterMode.Ignore
        };

        AddChild(_container);
    }

    public void UpdateFromComponent(ComboHelperComponent component)
    {
        // Очищуємо попередній контент
        _container.DisposeAllChildren();

        // Якщо віджет вимкнено або немає прототипу - ховаємо
        if (!component.Enabled || component.Prototype == null)
        {
            Visible = false;
            return;
        }

        // Намагаємося отримати прототип helper
        if (!_proto.TryIndex(component.Prototype.Value, out CqcComboHelperPrototype? helperProto))
        {
            Visible = false;
            return;
        }

        // Якщо немає комбо - ховаємо
        if (helperProto.Combos.Count == 0)
        {
            Visible = false;
            return;
        }

        Visible = true;

        // Створюємо UI для кожного комбо
        foreach (var entry in helperProto.Combos)
        {

            if (!_proto.TryIndex(entry.ComboId, out ComboPrototype? comboProto))
            {
                continue;
            }

            CreateComboRow(entry, comboProto);
        }
    }

    private void CreateComboRow(ComboHelperEntry entry, ComboPrototype comboProto)
    {
        // Створюємо горизонтальний рядок для комбо
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore
        };

        // Додаємо назву комбо
        var labelText = string.IsNullOrEmpty(entry.Name)
            ? Loc.GetString(comboProto.Name)
            : Loc.GetString(entry.Name);

        var label = new Label
        {
            Text = labelText,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore
        };
        row.AddChild(label);

        // Використовуємо кастомні іконки якщо є, інакше генеруємо з типів атак
        var icons = entry.Icons;
        if (icons == null || icons.Count == 0)
        {
            icons = GetDefaultIcons(comboProto.AttackTypes);
        }

        // Додаємо іконки
        foreach (var icon in icons)
        {
            var texture = TryGetTexture(icon);
            if (texture == null)
            {
                continue;
            }

            var rect = new TextureRect
            {
                TextureScale = new Vector2(0.5f, 0.5f),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(24, 24),
                Texture = texture,
                MouseFilter = MouseFilterMode.Ignore
            };
            row.AddChild(rect);
        }

        _container.AddChild(row);
    }

    private Texture? TryGetTexture(SpriteSpecifier icon)
    {
        try
        {
            return _spriteSystem.Frame0(icon);
        }
        catch (Exception ex)
        {
            Logger.ErrorS("ComboHelper", $"Failed to load texture: {ex.Message}");
            return null;
        }
    }

    private List<SpriteSpecifier> GetDefaultIcons(List<ComboAttackType> attacks)
    {
        var icons = new List<SpriteSpecifier>();
        var basePath = new ResPath("/Textures/_Goobstation/Interface/Misc/intents.rsi");

        foreach (var attack in attacks)
        {
            var state = attack switch
            {
                ComboAttackType.Grab => "grab",
                ComboAttackType.Disarm => "disarm",
                ComboAttackType.Harm => "harm",
                _ => "help"
            };

            icons.Add(new SpriteSpecifier.Rsi(basePath, state));
        }

        return icons;
    }
}
