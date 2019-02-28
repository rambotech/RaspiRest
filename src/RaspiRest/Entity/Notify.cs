using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace RaspiRest.Entity
{
    public class Notify
    {
        public Dictionary<string, WebhookAction> WebHookActions { get; set; } = new Dictionary<string, WebhookAction>();
        public Dictionary<string, EmailAction> EmailActions { get; set; } = new Dictionary<string, EmailAction>();

        public string FromAddress { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public bool SmtpIsSSl { get; set; }
        public string SmtpLogin { get; set; }
        public string SmtpLoginPassword { get; set; }

        public Notify(IConfiguration config)
        {
            FromAddress = config.GetValue<string>("FromAddress");
            SmtpServer = config.GetValue<string>("SmtpServer");
            SmtpPort = config.GetValue<int>("SmtpPort");
            SmtpIsSSl = config.GetValue<bool>("SmtpIsSSl");
            SmtpLogin = config.GetValue<string>("SmtpLogin");
            SmtpLoginPassword = config.GetValue<string>("SmtpLoginPassword");
        }
    }
}
