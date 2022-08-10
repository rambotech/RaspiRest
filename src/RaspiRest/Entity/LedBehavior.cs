using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Device.Gpio;

namespace RaspiRest.Entity
{
    public class LedBehavior
    {
        public string Name { get; set; }
        public PinNumberingScheme PinScheme { get; set; }
        public int Pin { get; set; }
        public Led.FlashMode FlashMode { get; set; }
        public Led.LightState Visibility { get; set; }
    }
}
