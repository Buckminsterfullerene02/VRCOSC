﻿using System;
using osu.Framework.Allocation;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using VRCOSC.Game.Graphics.Containers.Module.ModulOscParameter;

namespace VRCOSC.Game.Graphics.Containers.Module;

public class ModuleContainer : Container
{
    public Modules.Module SourceModule { get; init; }

    private FillFlowContainer<ModuleSettingContainer> settingsFlow { get; set; }
    private FillFlowContainer<ModuleOscParameterContainer> parameterFlow { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        if (SourceModule == null)
            throw new ArgumentNullException(nameof(SourceModule));

        Children = new Drawable[]
        {
            new Box
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Colour = VRCOSCColour.Gray3,
            },
            new Container
            {
                Name = "Content",
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Vertical = 10,
                    Horizontal = 30
                },
                Child = new BasicScrollContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    ClampExtension = 20,
                    ScrollbarVisible = false,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Spacing = new Vector2(0, 10),
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new TextFlowContainer(t => t.Font = FrameworkFont.Regular.With(size: 75))
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                TextAnchor = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = SourceModule.Title
                            },
                            new TextFlowContainer(t =>
                            {
                                t.Font = FrameworkFont.Regular.With(size: 30);
                                t.Colour = VRCOSCColour.Gray9;
                            })
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                TextAnchor = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = SourceModule.Description
                            },
                            new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                Size = new Vector2(0.95f, 5),
                                Masking = true,
                                CornerRadius = 4,
                                Child = new Box
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = VRCOSCColour.Gray1,
                                }
                            },
                            new TextFlowContainer(t => t.Font = FrameworkFont.Regular.With(size: 50))
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                TextAnchor = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = "Settings"
                            },
                            settingsFlow = new FillFlowContainer<ModuleSettingContainer>
                            {
                                Name = "Settings",
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 5)
                            },
                            new Container
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                Size = new Vector2(0.95f, 5),
                                Masking = true,
                                CornerRadius = 4,
                                Child = new Box
                                {
                                    Anchor = Anchor.Centre,
                                    Origin = Anchor.Centre,
                                    RelativeSizeAxes = Axes.Both,
                                    Colour = VRCOSCColour.Gray1,
                                }
                            },
                            new TextFlowContainer(t => t.Font = FrameworkFont.Regular.With(size: 50))
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                TextAnchor = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Text = "Values"
                            },
                            parameterFlow = new FillFlowContainer<ModuleOscParameterContainer>
                            {
                                Name = "Values",
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 5)
                            }
                        }
                    }
                }
            }
        };

        SourceModule.Settings.Keys.ForEach(key =>
        {
            var (settingType, _) = SourceModule.Settings[key];

            if (settingType == typeof(string))
            {
                settingsFlow.Add(new ModuleSettingStringContainer
                {
                    Key = key,
                    SourceModule = SourceModule
                });
            }
            else if (settingType == typeof(bool))
            {
                settingsFlow.Add(new ModuleSettingBoolContainer
                {
                    Key = key,
                    SourceModule = SourceModule
                });
            }
            else if (settingType == typeof(int))
            {
                settingsFlow.Add(new ModuleSettingIntContainer
                {
                    Key = key,
                    SourceModule = SourceModule
                });
            }
        });
    }
}
