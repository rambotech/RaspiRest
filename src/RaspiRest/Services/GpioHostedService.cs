using System;
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
#if USE_GPIO
        private readonly GpioController _controlGPIO;
#endif
        private Timer _timer;
        private Flash _flash;
        private PinValue _currentPinValue = PinValue.Low;

        public GpioHostedService(ILogger<GpioHostedService> logger, Flash flash)
        {
            _logger = logger;
            _flash = flash;
#if USE_GPIO
            _controlGPIO = new GpioController(_flash.PinScheme);
            _controlGPIO.OpenPin(_flash.LedPin);
            _controlGPIO.SetPinMode(_flash.LedPin, PinMode.Output);
            _controlGPIO.Write(_flash.LedPin, _currentPinValue);
#endif
        }

        ~GpioHostedService()
        {
#if USE_GPIO
            _controlGPIO.ClosePin(_flash.LedPin);
            _controlGPIO.Dispose();
#endif
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GPIO Background Service is starting.");
            _flash.LightVisibility = Flash.LightState.Off;
            _flash.FlashingMode = Flash.FlashMode.Slow;
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            _logger.LogInformation("GPIO Background Service is working.");
            switch (_flash.LightVisibility)
            {
                case Flash.LightState.On:
                    if (_currentPinValue == PinValue.Low)
                    {
                        _logger.LogInformation("GPIO Background Service:LED to ON");
                        _currentPinValue = PinValue.High;
#if USE_GPIO
                        try
                        {
                            _controlGPIO.Write(_flash.LedPin, _currentPinValue);
                        }
                        catch
                        {

                        }
#endif
                    }
                    _timer.Change(3000, -1);
                    break;

                case Flash.LightState.Off:
                    if (_currentPinValue == PinValue.High)
                    {
                        _logger.LogInformation("GPIO Background Service:LED to OFF");
                        _currentPinValue = PinValue.Low;
#if USE_GPIO
                        try
                        {
                            _controlGPIO.Write(_flash.LedPin, _currentPinValue);
                        }
                        catch
                        {

                        }
#endif
                    }
                    _timer.Change(3000, -1);
                    break;

                case Flash.LightState.Flashing:
                    if (_flash.FlashIndex >= _flash.FlashTiming[_flash.FlashingMode].Length)
                    {
                        _flash.FlashIndex = 0;
                    }
                    var pinNewState = ((_flash.FlashIndex & 1) == 0) ? PinValue.High : PinValue.Low;
                    var delayMS = _flash.FlashTiming[_flash.FlashingMode][_flash.FlashIndex];
                    if (_currentPinValue != pinNewState)
                    {
                        _currentPinValue = pinNewState;
                        _logger.LogInformation(
                            string.Format("GPIO Background Service: LED to {0}, Delay = {1}", 
                                _currentPinValue == PinValue.High ? "ON" : "OFF", 
                                delayMS));
#if USE_GPIO
                        try
                        {
                            _controlGPIO.Write(_flash.LedPin, pinNewState);
                        }
                        catch
                        {

                        }
#endif
                    }
                    _logger.LogInformation($"GPIO Flash event: State: {pinNewState}");
                    _flash.FlashIndex++;
                    if (_flash.FlashIndex >= _flash.FlashTiming[_flash.FlashingMode].Length)
                    {
                        _flash.FlashIndex = 0;
                    }
                    _timer.Change(delayMS, -1);
                    break;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("GPIO Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
