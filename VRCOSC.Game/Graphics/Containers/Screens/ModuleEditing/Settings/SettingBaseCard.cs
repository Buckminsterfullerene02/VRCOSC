﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using VRCOSC.Game.Modules;

namespace VRCOSC.Game.Graphics.Containers.Screens.ModuleEditing.Settings;

public class SettingBaseCard : AttributeCard
{
    protected readonly ModuleAttributeData attributeData;

    public SettingBaseCard(ModuleAttributeData attributeData)
    {
        this.attributeData = attributeData;
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        Height = 100;

        TextFlow.AddText(attributeData.DisplayName, t =>
        {
            t.Font = FrameworkFont.Regular.With(size: 30);
        });
        TextFlow.AddParagraph(attributeData.Description, t =>
        {
            t.Font = FrameworkFont.Regular.With(size: 20);
            t.Colour = VRCOSCColour.Gray9;
        });
    }
}
