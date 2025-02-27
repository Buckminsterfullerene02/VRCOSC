﻿// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using LibreHardwareMonitor.Hardware;
using osu.Framework.Extensions.IEnumerableExtensions;
using VRCOSC.Modules.HardwareStats.Provider;

// ReSharper disable InconsistentNaming

namespace VRCOSC.Modules.HardwareStats;

public sealed class HardwareStatsProvider
{
    private readonly Computer computer;

    public bool CanAcceptQueries { get; private set; }

    public readonly List<CPU> CPUs = new();
    public readonly List<GPU> GPUs = new();
    public readonly RAM RAM = new();

    public HardwareStatsProvider()
    {
        computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };

        Task.Run(() =>
        {
            computer.Open();
            CanAcceptQueries = true;
        });
    }

    public void Update()
    {
        computer.Hardware.ForEach(updateHardware);
    }

    private void updateHardware(IHardware hardware)
    {
        hardware.Update();
        hardware.SubHardware.ForEach(updateHardware);

        var type = decypherComponent(hardware.Identifier.ToString());

        switch (type)
        {
            case CPU cpu:
                hardware.Sensors.ForEach(sensor => handleCPU(cpu, sensor));
                break;

            case GPU gpu:
                hardware.Sensors.ForEach(sensor => handleGPU(gpu, sensor));
                break;

            case RAM ram:
                hardware.Sensors.ForEach(sensor => handleRAM(ram, sensor));
                break;
        }
    }

    private Component decypherComponent(string address)
    {
        int index = 0;

        try
        {
            index = int.Parse(address.Split('/').Last());
        }
        catch (FormatException) { }

        if (address.Contains("cpu", StringComparison.InvariantCultureIgnoreCase))
        {
            while (index >= CPUs.Count)
            {
                CPUs.Add(new CPU());
            }

            return CPUs[index];
        }

        if (address.Contains("gpu", StringComparison.InvariantCultureIgnoreCase))
        {
            while (index >= GPUs.Count)
            {
                GPUs.Add(new GPU());
            }

            return GPUs[index];
        }

        if (address.Contains("ram", StringComparison.InvariantCultureIgnoreCase))
        {
            return RAM;
        }

        throw new InvalidOperationException("Could not find correct component to audit");
    }

    private void handleCPU(CPU cpu, ISensor sensor)
    {
        switch (sensor.SensorType)
        {
            case SensorType.Load:
                switch (sensor.Name)
                {
                    case @"CPU Total":
                        cpu.Usage = sensor.Value ?? 0f;
                        break;
                }

                break;

            case SensorType.Temperature:
                switch (sensor.Name)
                {
                    // AMD
                    case @"Core (Tctl/Tdie)":
                    // Intel
                    case @"CPU Package":
                        cpu.Temperature = (int?)sensor.Value ?? 0;
                        break;
                }

                break;
        }
    }

    private void handleGPU(GPU gpu, ISensor sensor)
    {
        switch (sensor.SensorType)
        {
            case SensorType.Load:
                switch (sensor.Name)
                {
                    case @"GPU Core":
                        gpu.Usage = sensor.Value ?? 0f;
                        break;
                }

                break;

            case SensorType.Temperature:
                switch (sensor.Name)
                {
                    case @"GPU Core":
                        gpu.Temperature = (int?)sensor.Value ?? 0;
                        break;
                }

                break;
        }
    }

    private void handleRAM(RAM ram, ISensor sensor)
    {
        switch (sensor.SensorType)
        {
            case SensorType.Load:
                switch (sensor.Name)
                {
                    case @"Memory":
                        ram.Usage = sensor.Value ?? 0f;
                        break;
                }

                break;

            case SensorType.Data:
                switch (sensor.Name)
                {
                    case @"Memory Used":
                        ram.Used = sensor.Value ?? 0f;
                        break;

                    case @"Memory Available":
                        ram.Available = sensor.Value ?? 0f;
                        break;
                }

                break;
        }
    }
}
