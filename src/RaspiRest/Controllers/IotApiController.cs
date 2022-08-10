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
using RaspiRest.Entity;

namespace RaspiRest.Controllers
{
    [Route("api/iot/v1")]
    [ApiController]
    public class IotApiController : ControllerBase
    {
        // The access key which allows requests from external clients to be honored and processed.
        // This is static, and should be fairly complex (e.g. GUID plus some extra prefix, infixes, and/or suffix.
        private string _SITE_AccessKey = "endpoint protection value from config";

        // If this, then that (IFTTT.comn) webhook prototype  (this server ==to==> IFTTT event)
        private string _IFTTT_BaseAddress = "https://maker.ifttt.com/trigger/{0}/with/key/{1}";

        // The webhook key to trigger an event within the IFTTT account.
        private string _IFTTT_WebhookAccessKey = "IFTTT-provided value from config";

        // injected
        private readonly ILogger _logger;
        private LedList _flashList;
        private Notify _notify;

        public IotApiController(ILoggerFactory loggerFactory, IConfiguration config, LedList flashList, Notify notify)
        {
            _logger = loggerFactory.CreateLogger("Misc");
            _flashList = flashList;
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
                // Triggers an email
                if (eventName == "email")
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

                // Triggers an event on IFTTT via a webhook
                if (eventName == "webhook")
                {
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: webhook (send an email)");

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

                // "set light {text}" ... value2 contains the spoken text.  It must be parsed for the light identifier, and the action.
                if (eventName == "led_control")
                {
                    var wordList = value2.Trim().ToLower().Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                    var flashMode = Led.FlashMode.Help;
                    var ledName = _flashList.LED[0].Name;
                    var visibility = Led.LightState.Flashing;
                    foreach (var item in wordList)
                    {
                        Enum.TryParse<Led.FlashMode>(item, out flashMode);
                        Enum.TryParse<Led.LightState>(item, out visibility);
                        var led = _flashList.LED.Where(t => t.Name == item).FirstOrDefault();
                        if (led != null)
                        {
                            ledName = led.Name;
                        }
                    }
                    var thisLed = _flashList.LED.Where(t => t.Name == ledName).FirstOrDefault();
                    thisLed.FlashingMode = flashMode;
                    thisLed.LightVisibility = visibility;

                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_on (LED on)");

                    return Ok("Accepted");
                }

                if (eventName == "led_off")
                {
                    _flash.LightVisibility = Led.LightState.Off;
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_off (LED off)");
                    return Ok("Accepted");
                }

                if (eventName == "led_toggle")
                {
                    _flash.LightVisibility = (
                            _flash.LightVisibility == Led.LightState.Flashing ? Led.LightState.Off :
                            (_flash.LightVisibility == Led.LightState.On ? Led.LightState.Off : Led.LightState.On)
                        );
                    _logger.LogWarning($"Request from: {Request.HttpContext.Connection.RemoteIpAddress}: Triggered event: led_toggle (LED toggle)");
                    return Ok("Accepted");
                }

                if (eventName == "led_flash")
                {
                    _flash.LightVisibility = Led.LightState.Flashing;
                    _flash.FlashingMode = Led.FlashMode.Fast;
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