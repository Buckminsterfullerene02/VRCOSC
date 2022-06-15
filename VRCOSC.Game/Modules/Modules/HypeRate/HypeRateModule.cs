﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using osu.Framework.Graphics;

namespace VRCOSC.Game.Modules.Modules.HypeRate;

public class HypeRateModule : Module
{
    public override string Title => "HypeRate";
    public override string Description => "Sends HypeRate.io heartrate values";
    public override string Author => "VolcanicArts";
    public override Colour4 Colour => Colour4.OrangeRed.Darken(0.5f);
    public override ModuleType Type => ModuleType.Health;
    public override string[] Tags => new[] { "heartrate" };

    private HypeRateProvider? hypeRateProvider;
    private bool receivedHeartrate;

    public override void CreateAttributes()
    {
        CreateSetting(HypeRateSettings.Id, "HypeRate ID", "Your HypeRate ID given on your device", string.Empty);

        CreateOutputParameter(HypeRateParameter.HeartrateEnabled, "Heartrate Enabled", "Whether this module is attempting to emit values", "/avatar/parameters/HeartrateEnabled");
        CreateOutputParameter(HypeRateParameter.HeartrateNormalised, "Heartrate Normalised", "The heartrate value normalised to 60bpm", "/avatar/parameters/HeartrateNormalised");
        CreateOutputParameter(HypeRateParameter.HeartrateUnits, "Heartrate Units", "The units digit 0-9 mapped to a float", "/avatar/parameters/HeartrateUnits");
        CreateOutputParameter(HypeRateParameter.HeartrateTens, "Heartrate Tens", "The tens digit 0-9 mapped to a float", "/avatar/parameters/HeartrateTens");
        CreateOutputParameter(HypeRateParameter.HeartrateHundreds, "Heartrate Hundreds", "The hundreds digit 0-9 mapped to a float", "/avatar/parameters/HeartrateHundreds");
    }

    public override void Start()
    {
        SendParameter(HypeRateParameter.HeartrateEnabled, false);

        var hypeRateId = GetSetting<string>(HypeRateSettings.Id);

        if (string.IsNullOrEmpty(hypeRateId))
        {
            Terminal.Log("Cannot connect to HypeRate. Please enter an Id");
            return;
        }

        hypeRateProvider = new HypeRateProvider(hypeRateId, VRCOSCSecrets.KEYS_HYPERATE);
        hypeRateProvider.OnHeartRateUpdate += handleHeartRateUpdate;
        hypeRateProvider.OnConnected += () => SendParameter(HypeRateParameter.HeartrateEnabled, true);
        hypeRateProvider.OnDisconnected += () => SendParameter(HypeRateParameter.HeartrateEnabled, false);
        hypeRateProvider.OnWsHeartbeat += handleWsHeartbeat;
        hypeRateProvider.Connect();
    }

    private void handleHeartRateUpdate(int heartrate)
    {
        receivedHeartrate = true;
        var normalisedHeartRate = heartrate / 60.0f;
        var individualValues = ModuleHelper.ToDigitArray(heartrate, 3);

        SendParameter(HypeRateParameter.HeartrateEnabled, true);
        SendParameter(HypeRateParameter.HeartrateNormalised, normalisedHeartRate);
        SendParameter(HypeRateParameter.HeartrateUnits, individualValues[2] / 10f);
        SendParameter(HypeRateParameter.HeartrateTens, individualValues[1] / 10f);
        SendParameter(HypeRateParameter.HeartrateHundreds, individualValues[0] / 10f);
    }

    private void handleWsHeartbeat()
    {
        if (!receivedHeartrate) SendParameter(HypeRateParameter.HeartrateEnabled, false);
        receivedHeartrate = false;
    }

    public override void Stop()
    {
        hypeRateProvider?.Disconnect();
    }
}

public enum HypeRateSettings
{
    Id
}

public enum HypeRateParameter
{
    HeartrateEnabled,
    HeartrateNormalised,
    HeartrateUnits,
    HeartrateTens,
    HeartrateHundreds
}
