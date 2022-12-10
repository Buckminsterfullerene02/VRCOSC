// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Extensions.EnumExtensions;
using osu.Framework.Extensions.IEnumerableExtensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using VRCOSC.Game.Modules.Util;
using VRCOSC.Game.Util;
using VRCOSC.OSC;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming

namespace VRCOSC.Game.Modules;

[SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
public abstract class Module : IOscListener
{
    private GameHost Host = null!;
    private Storage Storage = null!;
    private OscClient OscClient = null!;
    private TerminalLogger Terminal = null!;
    protected Bindable<ModuleState> State = null!;
    private TimedTask? updateTask;
    private ChatBox ChatBox = null!;

    public readonly BindableBool Enabled = new();
    public readonly Dictionary<string, ModuleAttribute> Settings = new();
    public readonly Dictionary<Enum, ParameterMetadata> Parameters = new();

    public virtual string Title => string.Empty;
    public virtual string Description => string.Empty;
    public virtual string Author => string.Empty;
    public virtual ModuleType ModuleType => ModuleType.General;
    public virtual string Prefab => string.Empty;
    protected virtual int DeltaUpdate => int.MaxValue;
    protected virtual int ChatBoxPriority => 0;

    protected Player Player = null!;
    protected OpenVRInterface OpenVrInterface = null!;

    public const float vrc_osc_update_rate = 20;
    public const int vrc_osc_delta_update = (int)(1f / vrc_osc_update_rate * 1000f);

    public void Initialise(GameHost host, Storage storage, OscClient oscClient, ChatBox chatBox, OpenVRInterface openVrInterface)
    {
        Host = host;
        Storage = storage;
        OscClient = oscClient;
        ChatBox = chatBox;
        Terminal = new TerminalLogger(GetType().Name);
        State = new Bindable<ModuleState>(ModuleState.Stopped);
        Player = new Player(OscClient);
        OpenVrInterface = openVrInterface;

        CreateAttributes();
        performLoad();

        State.ValueChanged += _ => Log(State.Value.ToString());
    }

    #region Properties

    public bool HasSettings => Settings.Any();
    public bool HasParameters => Parameters.Any();

    private bool IsEnabled => Enabled.Value;
    private bool ShouldUpdate => DeltaUpdate != int.MaxValue;
    private string FileName => @$"{GetType().Name}.ini";

    protected bool IsStarting => State.Value == ModuleState.Starting;
    protected bool HasStarted => State.Value == ModuleState.Started;
    protected bool IsStopping => State.Value == ModuleState.Stopping;
    protected bool HasStopped => State.Value == ModuleState.Stopped;

    public const string VRChatOscPrefix = @"/avatar/parameters/";

    #endregion

    #region Attributes

    protected virtual void CreateAttributes() { }

    protected void CreateSetting(Enum lookup, string displayName, string description, bool defaultValue, Func<bool>? dependsOn = null)
        => addSingleSetting(lookup, displayName, description, defaultValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, int defaultValue, Func<bool>? dependsOn = null)
        => addSingleSetting(lookup, displayName, description, defaultValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, string defaultValue, Func<bool>? dependsOn = null)
        => addSingleSetting(lookup, displayName, description, defaultValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, Enum defaultValue, Func<bool>? dependsOn = null)
        => addSingleSetting(lookup, displayName, description, defaultValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, int defaultValue, int minValue, int maxValue, Func<bool>? dependsOn = null)
        => addRangedSetting(lookup, displayName, description, defaultValue, minValue, maxValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, float defaultValue, float minValue, float maxValue, Func<bool>? dependsOn = null)
        => addRangedSetting(lookup, displayName, description, defaultValue, minValue, maxValue, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, IEnumerable<int> defaultValues, bool canBeEmpty, Func<bool>? dependsOn = null)
        => addEnumerableSetting(lookup, displayName, description, defaultValues, canBeEmpty, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, IEnumerable<string> defaultValues, bool canBeEmpty, Func<bool>? dependsOn = null)
        => addEnumerableSetting(lookup, displayName, description, defaultValues, canBeEmpty, dependsOn);

    protected void CreateSetting(Enum lookup, string displayName, string description, string defaultValue, string buttonText, Action buttonAction, Func<bool>? dependsOn = null)
        => addTextAndButtonSetting(lookup, displayName, description, defaultValue, buttonText, buttonAction, dependsOn);

    protected void CreateParameter<T>(Enum lookup, ParameterMode mode, string parameterName, string description)
    {
        Parameters.Add(lookup, new ParameterMetadata(mode, parameterName, description, typeof(T)));
    }

    private void addSingleSetting(Enum lookup, string displayName, string description, object defaultValue, Func<bool>? dependsOn)
    {
        Settings.Add(lookup.ToLookup(), new ModuleAttributeSingle(new ModuleAttributeMetadata(displayName, description), defaultValue, dependsOn));
    }

    private void addEnumerableSetting<T>(Enum lookup, string displayName, string description, IEnumerable<T> defaultValues, bool canBeEmpty, Func<bool>? dependsOn)
    {
        Settings.Add(lookup.ToLookup(), new ModuleAttributeList(new ModuleAttributeMetadata(displayName, description), defaultValues.Cast<object>(), typeof(T), canBeEmpty, dependsOn));
    }

    private void addRangedSetting<T>(Enum lookup, string displayName, string description, T defaultValue, T minValue, T maxValue, Func<bool>? dependsOn) where T : struct
    {
        Settings.Add(lookup.ToLookup(), new ModuleAttributeSingleWithBounds(new ModuleAttributeMetadata(displayName, description), defaultValue, minValue, maxValue, dependsOn));
    }

    private void addTextAndButtonSetting(Enum lookup, string displayName, string description, string defaultValue, string buttonText, Action buttonAction, Func<bool>? dependsOn)
    {
        Settings.Add(lookup.ToLookup(), new ModuleAttributeSingleWithButton(new ModuleAttributeMetadata(displayName, description), defaultValue, buttonText, buttonAction, dependsOn));
    }

    #endregion

    #region Events

    internal async Task start(CancellationToken cancellationToken)
    {
        if (!IsEnabled) return;

        State.Value = ModuleState.Starting;
        Player.Init();

        await OnStart(cancellationToken);

        if (ShouldUpdate) updateTask = new TimedTask(OnUpdate, DeltaUpdate, true);
        await (updateTask?.Start() ?? Task.CompletedTask);

        OscClient.RegisterListener(this);

        State.Value = ModuleState.Started;
    }

    internal async Task stop()
    {
        if (!IsEnabled) return;

        State.Value = ModuleState.Stopping;

        OscClient.DeRegisterListener(this);

        if (updateTask is not null) await updateTask.Stop();

        await OnStop();

        Player.ResetAll();
        State.Value = ModuleState.Stopped;
    }

    protected virtual Task OnStart(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnUpdate() => Task.CompletedTask;
    protected virtual Task OnStop() => Task.CompletedTask;
    protected virtual void OnAvatarChange() { }
    protected virtual void OnPlayerStateUpdate(VRChatInputParameter key) { }

    #endregion

    #region Settings

    protected T GetSetting<T>(Enum lookup)
    {
        var setting = Settings[lookup.ToLookup()];

        object? value;

        switch (setting)
        {
            case ModuleAttributeSingle settingSingle:
                value = settingSingle.Attribute.Value;
                break;

            case ModuleAttributeList settingList when settingList.Type == typeof(string):
                value = settingList.GetValueList<string>();
                break;

            case ModuleAttributeList settingList when settingList.Type == typeof(int):
                value = settingList.GetValueList<int>();
                break;

            default:
                value = null;
                break;
        }

        if (value is not T valueCast)
            throw new InvalidCastException($"Setting with lookup '{lookup}' is not of type '{nameof(T)}'");

        return valueCast;
    }

    #endregion

    #region Parameters

    void IOscListener.OnDataSent(OscData data) { }

    void IOscListener.OnDataReceived(OscData data)
    {
        if (!data.Values.Any()) return;

        var address = data.Address;
        var value = data.Values[0];

        if (address.StartsWith("/avatar/change"))
        {
            OnAvatarChange();
            return;
        }

        if (!address.StartsWith(VRChatOscPrefix)) return;

        var parameterName = address.Remove(0, VRChatOscPrefix.Length);
        updatePlayerState(parameterName, value);

        try
        {
            Enum lookup = Parameters.Single(pair => pair.Value.Name == parameterName).Key;
            var parameterData = Parameters[lookup];

            if (!parameterData.Mode.HasFlagFast(ParameterMode.Read)) return;

            if (value.GetType() != parameterData.ExpectedType)
            {
                Log($@"Cannot accept input parameter. `{lookup}` expects type `{parameterData.ExpectedType}` but received type `{value.GetType()}`");
                return;
            }

            notifyParameterReceived(lookup, value, parameterData);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void notifyParameterReceived(Enum key, object value, ParameterMetadata data)
    {
        switch (value)
        {
            case bool boolValue:
                OnBoolParameterReceived(key, boolValue);
                break;

            case int intValue:
                OnIntParameterReceived(key, intValue);
                break;

            case float floatValue:
                OnFloatParameterReceived(key, floatValue);
                break;
        }
    }

    private void updatePlayerState(string parameterName, object value)
    {
        if (!Enum.TryParse(parameterName, out VRChatInputParameter vrChatInputParameter)) return;

        switch (vrChatInputParameter)
        {
            case VRChatInputParameter.Viseme:
                Player.Viseme = (Viseme)(int)value;
                break;

            case VRChatInputParameter.Voice:
                Player.Voice = (float)value;
                break;

            case VRChatInputParameter.GestureLeft:
                Player.GestureLeft = (Gesture)(int)value;
                break;

            case VRChatInputParameter.GestureRight:
                Player.GestureRight = (Gesture)(int)value;
                break;

            case VRChatInputParameter.GestureLeftWeight:
                Player.GestureLeftWeight = (float)value;
                break;

            case VRChatInputParameter.GestureRightWeight:
                Player.GestureRightWeight = (float)value;
                break;

            case VRChatInputParameter.AngularY:
                Player.AngularY = (float)value;
                break;

            case VRChatInputParameter.VelocityX:
                Player.VelocityX = (float)value;
                break;

            case VRChatInputParameter.VelocityY:
                Player.VelocityY = (float)value;
                break;

            case VRChatInputParameter.VelocityZ:
                Player.VelocityZ = (float)value;
                break;

            case VRChatInputParameter.Upright:
                Player.Upright = (float)value;
                break;

            case VRChatInputParameter.Grounded:
                Player.Grounded = (bool)value;
                break;

            case VRChatInputParameter.Seated:
                Player.Seated = (bool)value;
                break;

            case VRChatInputParameter.AFK:
                Player.AFK = (bool)value;
                break;

            case VRChatInputParameter.TrackingType:
                Player.TrackingType = (TrackingType)(int)value;
                break;

            case VRChatInputParameter.VRMode:
                Player.IsVR = (int)value == 1;
                break;

            case VRChatInputParameter.MuteSelf:
                Player.IsMuted = (bool)value;
                break;

            case VRChatInputParameter.InStation:
                Player.InStation = (bool)value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(vrChatInputParameter), vrChatInputParameter, "Unknown VRChatInputParameter");
        }

        OnPlayerStateUpdate(vrChatInputParameter);
    }

    protected virtual void OnBoolParameterReceived(Enum key, bool value) { }
    protected virtual void OnIntParameterReceived(Enum key, int value) { }
    protected virtual void OnFloatParameterReceived(Enum key, float value) { }

    protected void SendParameter<T>(Enum lookup, T value) where T : struct
    {
        if (HasStopped) return;

        if (!Parameters.ContainsKey(lookup)) throw new InvalidOperationException($"Parameter {lookup.ToString()} has not been defined");

        var data = Parameters[lookup];
        if (!data.Mode.HasFlagFast(ParameterMode.Write)) throw new InvalidOperationException("Cannot send a value to a read-only parameter");

        OscClient.SendValue(data.FormattedAddress, value);
    }

    #endregion

    #region Loading

    private void performLoad()
    {
        using (var stream = Storage.GetStream(FileName))
        {
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);

                while (reader.ReadLine() is { } line)
                {
                    switch (line)
                    {
                        case "#InternalSettings":
                            performInternalSettingsLoad(reader);
                            break;

                        case "#Settings":
                            performSettingsLoad(reader);
                            break;
                    }
                }
            }
        }

        executeAfterLoad();
    }

    private void performInternalSettingsLoad(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.Equals("#End")) break;

            var lineSplit = line.Split(new[] { '=' }, 2);
            var lookup = lineSplit[0];
            var value = lineSplit[1];

            switch (lookup)
            {
                case "enabled":
                    Enabled.Value = bool.Parse(value);
                    break;
            }
        }
    }

    private void performSettingsLoad(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            if (line.Equals("#End")) break;

            var lineSplitLookupValue = line.Split(new[] { '=' }, 2);
            var lookupType = lineSplitLookupValue[0].Split(new[] { ':' }, 2);
            var value = lineSplitLookupValue[1];

            var lookupStr = lookupType[0];
            var typeStr = lookupType[1];

            var lookup = lookupStr;
            if (lookupStr.Contains('#')) lookup = lookupStr.Split(new[] { '#' }, 2)[0];

            if (!Settings.ContainsKey(lookup)) continue;

            var setting = Settings[lookup];

            switch (setting)
            {
                case ModuleAttributeSingle settingSingle:
                {
                    var readableTypeName = settingSingle.Attribute.Value.GetType().ToReadableName().ToLowerInvariant();
                    if (!readableTypeName.Equals(typeStr)) continue;

                    switch (typeStr)
                    {
                        case "enum":
                            var typeAndValue = value.Split(new[] { '#' }, 2);
                            var enumType = enumNameToType(typeAndValue[0]);
                            if (enumType is not null) settingSingle.Attribute.Value = Enum.ToObject(enumType, int.Parse(typeAndValue[1]));
                            break;

                        case "string":
                            settingSingle.Attribute.Value = value;
                            break;

                        case "int":
                            settingSingle.Attribute.Value = int.Parse(value);
                            break;

                        case "float":
                            settingSingle.Attribute.Value = float.Parse(value);
                            break;

                        case "bool":
                            settingSingle.Attribute.Value = bool.Parse(value);
                            break;

                        default:
                            Logger.Log($"Unknown type found in file: {typeStr}");
                            break;
                    }

                    break;
                }

                case ModuleAttributeList settingList:
                {
                    if (value == "EMPTY" && settingList.CanBeEmpty)
                    {
                        settingList.AttributeList.Clear();
                        return;
                    }

                    var readableTypeName = settingList.AttributeList.First().Value.GetType().ToReadableName().ToLowerInvariant();
                    if (!readableTypeName.Equals(typeStr)) continue;

                    var index = int.Parse(lookupStr.Split(new[] { '#' }, 2)[1]);

                    switch (typeStr)
                    {
                        case "string":
                            settingList.AddAt(index, new Bindable<object>(value));
                            break;

                        case "int":
                            settingList.AddAt(index, new Bindable<object>(int.Parse(value)));
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(value), value, $"Unknown type found in file for {nameof(ModuleAttributeList)}: {value.GetType()}");
                    }

                    break;
                }
            }
        }
    }

    private void executeAfterLoad()
    {
        performSave();

        Enabled.BindValueChanged(_ => performSave());
        Settings.Values.ForEach(handleAttributeBind);
    }

    private void handleAttributeBind(ModuleAttribute value)
    {
        switch (value)
        {
            case ModuleAttributeSingle valueSingle:
                valueSingle.Attribute.BindValueChanged(_ => performSave());
                break;

            case ModuleAttributeList valueList:
                valueList.AttributeList.BindCollectionChanged((_, e) =>
                {
                    if (e.NewItems is not null)
                    {
                        foreach (var newItem in e.NewItems)
                        {
                            var bindable = (Bindable<object>)newItem;
                            bindable.BindValueChanged(_ => performSave());
                        }
                    }

                    performSave();
                });
                break;
        }
    }

    private static Type? enumNameToType(string enumName) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(enumName)).FirstOrDefault(type => type?.IsEnum ?? false);

    #endregion

    #region Saving

    private void performSave()
    {
        using var stream = Storage.CreateFileSafely(FileName);
        using var writer = new StreamWriter(stream);

        performInternalSettingsSave(writer);
        performSettingsSave(writer);
    }

    private void performInternalSettingsSave(TextWriter writer)
    {
        writer.WriteLine(@"#InternalSettings");
        writer.WriteLine(@"{0}={1}", "enabled", Enabled.Value.ToString());
        writer.WriteLine(@"#End");
    }

    private void performSettingsSave(TextWriter writer)
    {
        var areAllDefault = Settings.All(pair => pair.Value.IsDefault());
        if (areAllDefault) return;

        writer.WriteLine(@"#Settings");

        foreach (var (lookup, moduleAttributeData) in Settings)
        {
            if (moduleAttributeData.IsDefault()) continue;

            switch (moduleAttributeData)
            {
                case ModuleAttributeSingle moduleAttributeSingle:
                {
                    var value = moduleAttributeSingle.Attribute.Value;
                    var valueType = value.GetType();
                    var readableTypeName = valueType.ToReadableName().ToLowerInvariant();

                    if (valueType.IsSubclassOf(typeof(Enum)))
                    {
                        var enumClass = valueType.FullName;
                        writer.WriteLine(@"{0}:{1}={2}#{3}", lookup, readableTypeName, enumClass, (int)value);
                    }
                    else
                    {
                        writer.WriteLine(@"{0}:{1}={2}", lookup, readableTypeName, value);
                    }

                    break;
                }

                case ModuleAttributeList moduleAttributeList:
                {
                    var values = moduleAttributeList.AttributeList.ToList();

                    if (!values.Any())
                    {
                        writer.WriteLine(@"{0}:EMPTY=EMPTY", lookup);
                    }
                    else
                    {
                        var valueType = values.First().Value.GetType();
                        var readableTypeName = valueType.ToReadableName().ToLowerInvariant();

                        for (int i = 0; i < values.Count; i++)
                        {
                            writer.WriteLine(@"{0}#{1}:{2}={3}", lookup, i, readableTypeName, values[i].Value);
                        }
                    }

                    break;
                }
            }
        }

        writer.WriteLine(@"#End");
    }

    #endregion

    #region Extensions

    protected void Log(string message) => Terminal.Log(message);

    protected void OpenUrlExternally(string Url) => Host.OpenUrlExternally(Url);

    protected DateTimeOffset SetChatBoxText(string? text, TimeSpan displayLength) => ChatBox.SetText(text, ChatBoxPriority, displayLength);

    protected void SetChatBoxTyping(bool typing) => ChatBox.SetTyping(typing);

    protected static float Map(float source, float sMin, float sMax, float dMin, float dMax) => dMin + (dMax - dMin) * ((source - sMin) / (sMax - sMin));

    #endregion

    public enum ModuleState
    {
        Starting,
        Started,
        Stopping,
        Stopped
    }
}
