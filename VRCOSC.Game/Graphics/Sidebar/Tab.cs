﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osuTK;

namespace VRCOSC.Game.Graphics.Sidebar;

public sealed class Tab : ClickableContainer
{
    private readonly Colour4 hoveredColour = Colour4.White.Opacity(0.25f);

    private Box background = null!;
    private TabPopover popover = null!;

    public Tabs AssociatedTab { get; init; }
    public IconUsage Icon { get; init; }

    [Resolved]
    private BindableBool modulesRunning { get; set; } = null!;

    [Resolved]
    private VRCOSCGame game { get; set; } = null!;

    [BackgroundDependencyLoader]
    private void load()
    {
        Children = new Drawable[]
        {
            background = new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Colour = VRCOSCColour.Invisible
            },
            new SpriteIcon
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Size = new Vector2(0.4f),
                FillMode = FillMode.Fit,
                Icon = Icon,
                Colour = Colour4.White.Opacity(0.75f)
            },
            popover = new TabPopover
            {
                Anchor = Anchor.CentreRight,
                Origin = Anchor.CentreLeft,
                PopoverAnchor = Anchor.CentreLeft,
                Child = new SpriteText
                {
                    Text = AssociatedTab.ToString(),
                    Colour = Colour4.White
                }
            }
        };
    }

    protected override void LoadComplete()
    {
        Action += () =>
        {
            if (modulesRunning.Value)
            {
                background.FlashColour(VRCOSCColour.Red, 500, Easing.OutCubic);
                return;
            }

            game.SelectedTab.Value = AssociatedTab;
        };
    }

    protected override bool OnHover(HoverEvent e)
    {
        background.FadeColour(hoveredColour, 250, Easing.OutCubic);
        popover.Show();
        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        base.OnHoverLost(e);
        background.FadeColour(VRCOSCColour.Invisible, 250, Easing.InCubic);
        popover.Hide();
    }
}
