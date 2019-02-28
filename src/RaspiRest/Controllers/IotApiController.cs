using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RaspiRest.Entity;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Net.Http;
using System.Threading;

namespace RaspiRest.Controllers
{
    [Route("api/iot/v1")]
    [ApiController]
    public class IotApiController : ControllerBase
    {
        // The access key which allows requests from external clients to be honored and processed.
        // This is static, and should be fairly complex (e.g. GUID plus some extra prefix, infixes , and/or suffix.
        private string _SITE_AccessKey = "endpoint protection value from config";

        // If this, then that (IFTTT.comn) webhook prototype  (this server ==to==> IFTTT event)
        private string _IFTTT_BaseAddress = "https://maker.ifttt.com/trigger/{0}/with/key/{1}";

        // johnmikeralph
        private string _IFTTT_WebhookAccessKey = "IFTTT-provided value from config";

        // injected
        private readonly ILogger _logger;
        private Flash _flash;
        private Notify _notify;

        public IotApiController(ILoggerFactory loggerFactory, IConfiguration config, Flash flash, Notify notify)
        {
            _logger = loggerFactory.CreateLogger("Misc");
            _flash = flash;
            _notify = notify;
            _SITE_AccessKey = config.GetValue<string>("SITE_AccessKey");
            _IFTTT_WebhookAccessKey = config.GetValue<string>("IFTTT_WebhookAccessKey");
        }

        ~IotApiController()
        {
        }

        // Trigger the action
        [HttpGet("ping", Name = "Heartbeat")]
        [ProducesResponseType(200)]
        public IActionResult Heartbeat()
        {
            return Ok("Available");
        }

        // Trigger the action
        [HttpGet("trigger/{event}/{access}", Name = "EventProcesor")]
        [ProducesResponseType(200)]
        [ProducesResponseType(400)]
        public IActionResult EventProcesor(
            [FromRoute(Name = "event")] string eventName,
            [FromRoute(Name = "access")] string accessKey,
            [FromQuery] string value1,
            [FromQuery] string value2,
            [FromQuery] string value3)
        {
            if (accessKey != _SITE_AccessKey)
            {
                _logger.LogError($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Invalid key: {accessKey}");
                return BadRequest();
            }

            try
            {
                if (eventName == "xo")
                {
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: xo (execute order), Value1: {value1}, Value2: {value2}, Value3: {value3}");

                    var recipients = new string[]
                    {
                        "johnmikeralph@gmail.com"
                    };
                    foreach (var recipient in recipients)
                    {
                        var emailAction = new EmailAction
                        {
                            Addresses = new string[] { recipient },
                            Subject = "Execute Request: Result",
                            Body = $"v1: {value1}\r\nv2: {value2}\r\nv2: {value3}\r\n"
                        };
                        _notify.EmailActions.Add(emailAction.Id, emailAction);
                        _logger.LogInformation($"EmailAction queued: {eventName} for {recipient}");
                    }

                    return Ok("Accepted");
                }

                if (eventName == "mdet1")
                {
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: motion1 (motion detector test)");

                    var webhookEvent = new WebhookAction
                    {
                        Url = string.Format(_IFTTT_BaseAddress, eventName + "_ack", _IFTTT_WebhookAccessKey),
                        Attempts = 0,
                        PerishSeconds = 120,
                        Payload = string.Empty
                    };
                    _notify.WebHookActions.Add(webhookEvent.Id, webhookEvent);
                    _logger.LogInformation($"Action queued: motion_detected ({webhookEvent.Id})");

                    var recipients = new string[]
                    {
                        "alarmbot@bitsofgenius.com",
                        "johnmikeralph@gmail.com"
                    };
                    foreach (var recipient in recipients)
                    {
                        var emailAction = new EmailAction
                        {
                            Addresses = new string[] { recipient },
                            Subject = "Motion Detector Test: Result",
                            Body = "A motion detector test was triggered by an incoming webhook"
                        };
                        _notify.EmailActions.Add(emailAction.Id, emailAction);
                        _logger.LogInformation($"EmailAction queued: {eventName} for {recipient}");
                    }
                    return Ok("Accepted");
                }

                if (eventName == "led_on")
                {
                    _flash.LightVisibility = Flash.LightState.On;
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_on (LED on)");

#if USE_GPIO
                    GpioController controller = new GpioController(_flash.PinScheme);
                    controller.OpenPin(_flash.LedPin, PinMode.Output);
                    controller.Write(_flash.LedPin, PinValue.High);
#endif
                    return Ok("Accepted");
                }

                if (eventName == "led_off")
                {
                    _flash.LightVisibility = Flash.LightState.Off;
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_off (LED off)");
                    return Ok("Accepted");
                }

                if (eventName == "led_toggle")
                {
                    _flash.LightVisibility = (
                            _flash.LightVisibility == Flash.LightState.Flashing ? Flash.LightState.Off :
                            (_flash.LightVisibility == Flash.LightState.On ? Flash.LightState.Off : Flash.LightState.On)
                        );
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_toggle (LED toggle)");
                    return Ok("Accepted");
                }

                if (eventName == "led_flash")
                {
                    _flash.LightVisibility = Flash.LightState.Flashing;
                    _flash.FlashingMode = Flash.FlashMode.Fast;
                    var phrase = value2.ToLower().Replace("\"", string.Empty).Trim();
                    if (_flash.FlashPhrases.ContainsKey(phrase))
                    {
                        _flash.FlashingMode = _flash.FlashPhrases[phrase];
                    }
                    _flash.FlashIndex = 0;
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_flash ({_flash.FlashingMode}) with phrase ({value2})");
                    return Ok("Accepted");
                }
                _logger.LogError($"Unknown event requested: {eventName}");
                return NotFound("Unknown request");
            }
            catch (Exception err)
            {
                _logger.LogCritical($"Unhandled exception: {err.Message}");
                return StatusCode(500, err.Message);
            }
        }
    }
}