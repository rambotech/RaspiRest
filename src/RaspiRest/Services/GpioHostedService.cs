using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RaspiRest.Entity;

namespace RaspiRest.Services
{
    internal class GpioHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _timerBlock;
        private readonly Dictionary<string, Led> _ledFlashList = new Dictionary<string, Led>();
        private readonly Dictionary<string, Timer> _ledTimerList = new Dictionary<string, Timer>();
        private readonly Dictionary<string, GpioController> _Gpio = new Dictionary<string, GpioController>();

        public GpioHostedService(IConfiguration config, ILogger<GpioHostedService> logger, Led flash)
        {
            _logger = logger;
            _timerBlock = new object();
            foreach (var led in config.GetValue<List<LedBehavior>>("LEDs"))
            {
                _ledFlashList.Add(led.Name, new Led(led.PinScheme, led.LedPin, led.Visibility, led.FlashMode));
                _ledTimerList.Add(led.Name, null);
#if USE_GPIO
                using (var g = new GpioController(led.PinScheme))
                {
                    g.OpenPin(led.LedPin);
                    g.SetPinMode(led.LedPin, PinMode.Input);
                    _ledFlashList[led.Name].CurrentPinValue = g.Read(led.LedPin);
                    g.ClosePin(led.LedPin);
                }
                _Gpio.Add(led.Name, new GpioController(led.PinScheme));
                _Gpio[led.Name].OpenPin(_ledFlashList[led.Name].LedPin);
                _Gpio[led.Name].SetPinMode(_ledFlashList[led.Name].LedPin, PinMode.Output);
#endif
            }
        }

        ~GpioHostedService()
        {
#if USE_GPIO
            foreach (var key in _ledFlashList.Keys)
            {
                _Gpio[key].ClosePin(_ledFlashList[key].LedPin);
                _Gpio[key].Dispose();
            }
#endif
        }

        public void Dispose()
        {
            foreach (var key in _ledTimerList.Keys)
            {
                _ledTimerList[key]?.Dispose();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GPIO Background Service is starting.");
            foreach (var key in _ledTimerList.Keys)
            {
                _ledTimerList[key] = new Timer(DoWork, key, TimeSpan.Zero, TimeSpan.FromSeconds(3));
            }
            _logger.LogInformation("GPIO Background Service started.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GPIO Background Service is stopping.");
            foreach (var key in _ledTimerList.Keys)
            {
                _ledTimerList[key]?.Change(Timeout.Infinite, 0);
            }
            _logger.LogInformation("GPIO Background Service stopped.");
            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            lock (_timerBlock)
            {
                var key = (string)state;

                _ledTimerList[key].Change(Timeout.Infinite, -1);
                _logger.LogInformation($"GPIO Background Service is working on {key} LED.");
                switch (_ledFlashList[key].LightVisibility)
                {
                    case Led.LightState.On:
                        if (_ledFlashList[key].CurrentPinValue == PinValue.Low)
                        {
                            _logger.LogInformation("GPIO Background Service: {key} LED to ON");
                            _ledFlashList[key].CurrentPinValue = PinValue.High;
#if USE_GPIO
                            try
                            {
                                _Gpio[key].Write(_ledFlashList[key].LedPin, _ledFlashList[key].CurrentPinValue);
                            }
                            catch
                            {

                            }
#endif
                        }
                        _ledTimerList[key].Change(3000, -1);
                        break;

                    case Led.LightState.Off:
                        if (_ledFlashList[key].CurrentPinValue == PinValue.High)
                        {
                            _logger.LogInformation("GPIO Background Service: {key} LED to OFF");
                            _ledFlashList[key].CurrentPinValue = PinValue.Low;
#if USE_GPIO
                            try
                            {
                                _Gpio[key].Write(_ledFlashList[key].LedPin, _ledFlashList[key].CurrentPinValue);
                            }
                            catch
                            {

                            }
#endif
                        }
                        _ledTimerList[key].Change(3000, -1);
                        break;

                    case Led.LightState.Flashing:
                        if (_ledFlashList[key].FlashIndex >= _ledFlashList[key].FlashTiming[_ledFlashList[key].FlashingMode].Length)
                        {
                            _ledFlashList[key].FlashIndex = 0;
                        }
                        var pinNewState = ((_ledFlashList[key].FlashIndex & 1) == 0) ? PinValue.High : PinValue.Low;
                        var delayMS = _ledFlashList[key].FlashTiming[_ledFlashList[key].FlashingMode][_ledFlashList[key].FlashIndex];
                        if (_ledFlashList[key].CurrentPinValue != pinNewState)
                        {
                            _ledFlashList[key].CurrentPinValue = pinNewState;
                            _logger.LogInformation(
                                string.Format("GPIO Background Service: {0} LED to {1}, Delay = {2}",
                                    key,
                                    _ledFlashList[key].CurrentPinValue == PinValue.High ? "ON" : "OFF",
                                    delayMS));
#if USE_GPIO
                            try
                            {
                                _Gpio[key].Write(_ledFlashList[key].LedPin, pinNewState);
                            }
                            catch
                            {

                            }
#endif
                        }
                        _logger.LogInformation($"GPIO Flash event: State: {pinNewState}");
                        _ledFlashList[key].FlashIndex++;
                        if (_ledFlashList[key].FlashIndex >= _ledFlashList[key].FlashTiming[_ledFlashList[key].FlashingMode].Length)
                        {
                            _ledFlashList[key].FlashIndex = 0;
                        }
                        _ledTimerList[key].Change(delayMS, -1);
                        break;
                }
            }
        }
    }
}
