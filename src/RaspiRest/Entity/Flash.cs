using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Device.Gpio;

namespace RaspiRest.Entity
{
    public class Flash
    {
        public int LedPin { get; set; } = 18;  // BCM 18 (board pin 12)
        public PinNumberingScheme PinScheme { get; set; } = PinNumberingScheme.Logical;

        public enum LightState : int
        {
            Off = 0,
            On = 1,
            Flashing = 2
        }

        public enum FlashMode : int
        {
            Beer  = 0,
            Slow = 1,
            Medium = 2,
            Fast = 3,
            Help = 4,
            Mayday = 5,
        };

        public LightState LightVisibility = LightState.Off;
        public FlashMode FlashingMode { get; set; } = FlashMode.Help;

        public int FlashIndex { get; set; } = 0;

        public Dictionary<string, FlashMode> FlashPhrases { get; private set; } = new Dictionary<string, FlashMode>
        {
            { "slow", FlashMode.Slow },
            { "medium", FlashMode.Medium },
            { "fast", FlashMode.Fast },
            { "help", FlashMode.Help },
            { "mayday", FlashMode.Mayday },
            { "beer", FlashMode.Beer }
        };

        public Dictionary<FlashMode, int[]> FlashTiming = new Dictionary<FlashMode, int[]>
        {
            { FlashMode.Slow, new int[] { 3000, 3000 } },
            { FlashMode.Medium, new int[] { 1000, 1000 } },
            { FlashMode.Fast, new int[] { 200, 200 } },
            { FlashMode.Help, new int[] {
                300, 300,
                300, 300,
                300, 900,
                900, 300,
                900, 300,
                900, 900,
                300, 300,
                300, 300,
                300, 2100 } },
            { FlashMode.Mayday, new int[] {
                150, 150,
                150, 150,
                150, 450,
                450, 150,
                450, 150,
                450, 450,
                150, 150,
                150, 150,
                150, 1050 } },
            { FlashMode.Beer, new int[] {
                450, 150,
                150, 150,
                150, 150,
                150, 450,
                150, 450,
                150, 450,
                150, 150,
                450, 150,
                150, 1050 }
            }
        };

        public Flash(IConfiguration config)
        {
            PinScheme = config.GetValue<PinNumberingScheme>("PinScheme");
            LedPin = config.GetValue<int>("PinLED");
        }
    }
}
