using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Device.Gpio;
using Microsoft.Extensions.Configuration;

namespace RaspiRest.Entity
{
    public class LedList
    {
        public List<Led> LED { get; set; } = new List<Led>();

        public LedList (IConfiguration config)
        {
            foreach (var led in config.GetValue<List<LedBehavior>>("LEDs"))
            {
                LED.Add(new Led(led.Name, led.PinScheme, led.Pin, led.Visibility, led.FlashMode));
            }
        }
    }
}
