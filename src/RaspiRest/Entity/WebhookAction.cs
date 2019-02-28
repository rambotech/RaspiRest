﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RaspiRest.Entity
{
    [JsonObject]
    public class WebhookAction
    {
        [JsonProperty]
        public string Id { get; private set; } = Guid.NewGuid().ToString("N").ToLower();

        [JsonProperty]
        public DateTime SubmittedOn { get; private set; } = DateTime.Now;

        [JsonProperty]
        public string Url { get; set; }

        [JsonProperty]
        public string Payload { get; set; }

        [JsonProperty]
        public DateTime NextAttempt { get; set; } = DateTime.MinValue;

        [JsonProperty]
        public int Attempts { get; set; } = 0;

        [JsonProperty]
        public int PerishSeconds { get; set; } = 120;

        [JsonProperty]
        public bool IsSending{ get; set; } = false;
    }
}
