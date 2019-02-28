using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using RaspiRest.Entity;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace RaspiRest.Services
{
    #region snippet1
    internal class NotifyHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly Notify _notify;

        private Timer _timer;
        private object WebHookEventLock = new object();
        private object EmailEventLock = new object();

        // Webhook retry wait times in seconds.
        private readonly int[] WebHookDefaultRetryDelaySeconds = new int[] { 2, 5, 15, 60, 300, 1800, 3600 };
        // Email retry wait times in seconds.
        private readonly int[] EmailDefaultRetryDelaySeconds = new int[] { 2, 5, 15, 60, 300, 1800, 3600 };

        public NotifyHostedService(ILogger<NotifyHostedService> logger, IConfiguration config, Notify notify)
        {
            _logger = logger;
            _notify = notify;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is starting.");
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            _logger.LogInformation("Timed Background Service is working.");
            var anyProcessed = ProcessWebHookQueue();
            anyProcessed |= ProcessEmailActionQueue();
            _timer.Change(anyProcessed ? 100 : 3000, -1);
        }

        private bool ProcessWebHookQueue()
        {
            var anyProcessed = false;
            if (_notify.WebHookActions.Count != 0)
            {
                lock (WebHookEventLock)
                {
                    // find a qualifying web hook action
                    var id = _notify.WebHookActions.Values
                        .Where(t => t.NextAttempt < DateTime.Now && !t.IsSending)
                        .Select(s => s.Id)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        anyProcessed = true;
                        _logger.LogInformation($"Sending webhook action {id}");
                        _notify.WebHookActions[id].IsSending = true;
                        _notify.WebHookActions[id].NextAttempt = DateTime.Now.AddSeconds(
                            WebHookDefaultRetryDelaySeconds[
                                Math.Min(
                                    _notify.WebHookActions[id].Attempts,
                                    WebHookDefaultRetryDelaySeconds.Length - 1)
                            ]);
                        _notify.WebHookActions[id].Attempts++;
                        SendWebhook(id);
                    }
                }
            }
            return anyProcessed;
        }

        public void SendWebhook(string id)
        {
            try
            {
                var http = new HttpClient();
                HttpResponseMessage resp = null;
                if (!string.IsNullOrWhiteSpace(_notify.WebHookActions[id].Payload))
                {
                    resp = http.PostAsJsonAsync(_notify.WebHookActions[id].Url, _notify.WebHookActions[id].Payload).GetAwaiter().GetResult();
                }
                else
                {
                    resp = http.GetAsync(_notify.WebHookActions[id].Url).GetAwaiter().GetResult();
                }
                resp.EnsureSuccessStatusCode();
                _logger.LogInformation($"Sent webhook action {id}");

                lock (WebHookEventLock)
                {
                    _notify.WebHookActions.Remove(id);
                }
            }
            catch (Exception err)
            {
                if (_notify.WebHookActions[id].NextAttempt > _notify.WebHookActions[id].SubmittedOn.AddSeconds(_notify.WebHookActions[id].PerishSeconds))
                {
                    _logger.LogWarning($"Failed (perish) webhook action {id}, {_notify.WebHookActions[id].Url}: {err.Message}");
                    return;
                }
                _logger.LogWarning($"Failed (retry) webhook action {id}, {_notify.WebHookActions[id].Url}: {err.Message}");
                lock (WebHookEventLock)
                {
                    _notify.WebHookActions[id].IsSending = false;
                }
            }
        }

        public bool ProcessEmailActionQueue()
        {
            var anyProcessed = false;
            if (_notify.EmailActions.Count != 0)
            {
                lock (EmailEventLock)
                {
                    // find a qualifying web hook action
                    var id = _notify.EmailActions.Values
                        .Where(t => t.NextAttempt < DateTime.Now && !t.IsSending)
                        .Select(s => s.Id)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        anyProcessed = true;
                        _logger.LogInformation($"Sending email action {id}");
                        _notify.EmailActions[id].IsSending = true;
                        _notify.EmailActions[id].NextAttempt = DateTime.Now.AddSeconds(
                            EmailDefaultRetryDelaySeconds[
                                Math.Min(
                                    _notify.EmailActions[id].Attempts,
                                    EmailDefaultRetryDelaySeconds.Length - 1)
                            ]);
                        _notify.EmailActions[id].Attempts++;
                        SendEmail(id);
                    }
                }
            }
            return anyProcessed;
        }

        private void SendEmail(string id)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_notify.FromAddress));
                foreach (var addr in _notify.EmailActions[id].Addresses)
                {
                    message.To.Add(new MailboxAddress(addr));
                }
                message.Subject = _notify.EmailActions[id].Subject;
                message.Body = new TextPart("plain") { Text = _notify.EmailActions[id].Body };

                using (var client = new MailKit.Net.Smtp.SmtpClient())
                {
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    client.Connect(_notify.SmtpServer, _notify.SmtpPort, _notify.SmtpIsSSl);

                    if (!string.IsNullOrWhiteSpace(_notify.SmtpLogin))
                    {
                        client.Authenticate(_notify.SmtpLogin, _notify.SmtpLoginPassword);
                    }
                    client.Send(message);
                    client.Disconnect(true);
                }
                _logger.LogInformation($"Sent email action {id}");

                lock (EmailEventLock)
                {
                    _notify.EmailActions.Remove(id);
                }
            }
            catch (Exception err)
            {
                if (_notify.EmailActions[id].NextAttempt > _notify.EmailActions[id].SubmittedOn.AddSeconds(_notify.EmailActions[id].PerishSeconds))
                {
                    _logger.LogWarning($"Failed (perish) email action {id}, {_notify.EmailActions[id].Body}: {err.Message}");
                    return;
                }
                _logger.LogWarning($"Failed (retry) email action {id}, {_notify.EmailActions[id].Body}: {err.Message}");
                lock (EmailEventLock)
                {
                    _notify.EmailActions[id].IsSending = false;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
    #endregion    
}
