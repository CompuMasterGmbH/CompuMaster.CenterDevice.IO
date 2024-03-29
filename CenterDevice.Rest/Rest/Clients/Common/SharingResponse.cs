﻿using System.Text.Json.Serialization;
using System.Collections.Generic;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Common
{
    public class SharingResponse
    {
        [JsonPropertyName(RestApiConstants.FAILED_GROUPS)]
        public List<string> FailedGroups { get; set; }

        [JsonPropertyName(RestApiConstants.FAILED_USERS)]
        public List<string> FailedUsers { get; set; }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element