using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

class Program
{
    static readonly Guid CSC_SERVICE =
        Guid.Parse("00001816-0000-1000-8000-00805f9b34fb");

    static readonly Guid CSC_MEASUREMENT =
        Guid.Parse("00002a5b-0000-1000-8000-00805f9b34fb");

    static BluetoothLEAdvertisementWatcher? watcher;
    static BluetoothLEDevice? device;
    static GattCharacteristic? measurementCharacteristic;

    // Change this to your actual wheel circumference in meters
    // 700x25c ≈ 2.105
    // 700x28c ≈ 2.136
    // 29er MTB ≈ 2.29
    static double wheelCircumferenceMeters = 2.105;

    static uint? lastWheelRevs = null;
    static ushort? lastWheelEventTime = null;

    static ushort? lastCrankRevs = null;
    static ushort? lastCrankEventTime = null;

    static uint? tripStartWheelRevs = null;

    static Queue<double> recentSpeeds = new Queue<double>();
    static int smoothingWindow = 5;

    static bool connected = false;

    static async Task Main()
    {
        Console.WriteLine("Cyclami CSC BLE Reader");
        Console.WriteLine("----------------------");
        Console.WriteLine($"Wheel circumference: {wheelCircumferenceMeters:F3} m");
        Console.WriteLine("Spin the wheel to wake the sensor.");
        Console.WriteLine("Scanning for CSC sensor...");
        Console.WriteLine("Press ENTER to quit.");
        Console.WriteLine();

        watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active
        };

        watcher.Received += Watcher_Received;
        watcher.Start();

        Console.ReadLine();

        try
        {
            watcher.Stop();
        }
        catch
        {
        }

        if (measurementCharacteristic != null)
        {
            try
            {
                measurementCharacteristic.ValueChanged -= Measurement_ValueChanged;
                await measurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            catch
            {
            }
        }

        device?.Dispose();
    }

    private static async void Watcher_Received(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        if (connected)
            return;

        try
        {
            if (!args.Advertisement.ServiceUuids.Contains(CSC_SERVICE))
                return;

            connected = true;
            sender.Stop();

            Console.WriteLine($"Found sensor at address: {args.BluetoothAddress}");

            device = await BluetoothLEDevice.FromBluetoothAddressAsync(args.BluetoothAddress);
            if (device == null)
            {
                Console.WriteLine("Failed to connect to device.");
                connected = false;
                return;
            }

            Console.WriteLine($"Connected to: {device.Name}");

            var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"Failed to get services. Status: {servicesResult.Status}");
                connected = false;
                return;
            }

            var cscService = servicesResult.Services.FirstOrDefault(s => s.Uuid == CSC_SERVICE);
            if (cscService == null)
            {
                Console.WriteLine("CSC service not found.");
                connected = false;
                return;
            }

            var characteristicsResult = await cscService.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
            if (characteristicsResult.Status != GattCommunicationStatus.Success)
            {
                Console.WriteLine($"Failed to get characteristics. Status: {characteristicsResult.Status}");
                connected = false;
                return;
            }

            measurementCharacteristic = characteristicsResult.Characteristics
                .FirstOrDefault(c => c.Uuid == CSC_MEASUREMENT);

            if (measurementCharacteristic == null)
            {
                Console.WriteLine("CSC Measurement characteristic not found.");
                connected = false;
                return;
            }

            measurementCharacteristic.ValueChanged += Measurement_ValueChanged;

            var notifyStatus =
                await measurementCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);

            if (notifyStatus == GattCommunicationStatus.Success)
            {
                Console.WriteLine("Subscribed to CSC notifications.");
                Console.WriteLine("Waiting for wheel movement...");
                Console.WriteLine();
            }
            else
            {
                Console.WriteLine($"Failed to enable notifications. Status: {notifyStatus}");
                connected = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Connection error: {ex.Message}");
            connected = false;
        }
    }

    private static void Measurement_ValueChanged(
        GattCharacteristic sender,
        GattValueChangedEventArgs args)
    {
        try
        {
            var reader = DataReader.FromBuffer(args.CharacteristicValue);

            // IMPORTANT: BLE characteristics are little-endian
            reader.ByteOrder = ByteOrder.LittleEndian;

            byte flags = reader.ReadByte();

            bool wheelDataPresent = (flags & 0x01) != 0;
            bool crankDataPresent = (flags & 0x02) != 0;

            double? speedKmh = null;
            double? cadenceRpm = null;
            double tripDistanceMeters = 0;

            if (wheelDataPresent)
            {
                uint cumulativeWheelRevs = reader.ReadUInt32();
                ushort currentWheelEventTime = reader.ReadUInt16();

                if (!tripStartWheelRevs.HasValue)
                {
                    tripStartWheelRevs = cumulativeWheelRevs;
                }

                tripDistanceMeters =
                    (cumulativeWheelRevs - tripStartWheelRevs.Value) * wheelCircumferenceMeters;

                if (lastWheelRevs.HasValue && lastWheelEventTime.HasValue)
                {
                    uint deltaRevs = cumulativeWheelRevs - lastWheelRevs.Value;

                    int deltaTicks = currentWheelEventTime >= lastWheelEventTime.Value
                        ? currentWheelEventTime - lastWheelEventTime.Value
                        : 65536 + currentWheelEventTime - lastWheelEventTime.Value;

                    if (deltaRevs > 0 && deltaTicks > 0)
                    {
                        double deltaSeconds = deltaTicks / 1024.0;
                        double metersPerSecond = (deltaRevs * wheelCircumferenceMeters) / deltaSeconds;
                        double rawKmh = metersPerSecond * 3.6;

                        // reject nonsense spikes
                        if (rawKmh >= 0 && rawKmh <= 100)
                        {
                            recentSpeeds.Enqueue(rawKmh);
                            while (recentSpeeds.Count > smoothingWindow)
                                recentSpeeds.Dequeue();

                            speedKmh = recentSpeeds.Average();
                        }
                    }
                    else if (deltaTicks > 0 && deltaRevs == 0)
                    {
                        recentSpeeds.Enqueue(0);
                        while (recentSpeeds.Count > smoothingWindow)
                            recentSpeeds.Dequeue();

                        speedKmh = recentSpeeds.Average();
                    }
                }

                lastWheelRevs = cumulativeWheelRevs;
                lastWheelEventTime = currentWheelEventTime;

                Console.WriteLine("---- WHEEL DATA ----");
                Console.WriteLine($"Wheel revolutions: {cumulativeWheelRevs}");
                Console.WriteLine($"Trip distance: {tripDistanceMeters:F1} m");

                if (speedKmh.HasValue)
                    Console.WriteLine($"Speed: {speedKmh.Value:F2} km/h");
                else
                    Console.WriteLine("Speed: waiting for second valid packet...");
            }

            if (crankDataPresent)
            {
                ushort cumulativeCrankRevs = reader.ReadUInt16();
                ushort currentCrankEventTime = reader.ReadUInt16();

                if (lastCrankRevs.HasValue && lastCrankEventTime.HasValue)
                {
                    int deltaRevs = cumulativeCrankRevs - lastCrankRevs.Value;

                    int deltaTicks = currentCrankEventTime >= lastCrankEventTime.Value
                        ? currentCrankEventTime - lastCrankEventTime.Value
                        : 65536 + currentCrankEventTime - lastCrankEventTime.Value;

                    if (deltaRevs > 0 && deltaTicks > 0)
                    {
                        double deltaSeconds = deltaTicks / 1024.0;
                        cadenceRpm = (deltaRevs / deltaSeconds) * 60.0;
                    }
                }

                lastCrankRevs = cumulativeCrankRevs;
                lastCrankEventTime = currentCrankEventTime;

                Console.WriteLine("---- CRANK DATA ----");
                Console.WriteLine($"Crank revolutions: {cumulativeCrankRevs}");

                if (cadenceRpm.HasValue)
                    Console.WriteLine($"Cadence: {cadenceRpm.Value:F1} RPM");
                else
                    Console.WriteLine("Cadence: waiting for second valid packet...");
            }

            Console.WriteLine("--------------------");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading measurement: {ex.Message}");
        }
    }
}