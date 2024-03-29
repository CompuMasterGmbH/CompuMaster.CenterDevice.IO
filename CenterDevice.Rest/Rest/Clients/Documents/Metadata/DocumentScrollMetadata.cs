﻿using System.Text.Json.Serialization;
using System;

#pragma warning disable CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element
namespace CenterDevice.Rest.Clients.Documents.Metadata
{
    public class DocumentScrollMetadata
    {
        public string Id { get; set; }

        [JsonPropertyName(RestApiConstants.VERSION_DATE)]
        public DateTime VersionDate { get; set; }
    }
}
#pragma warning restore CS1591 // Fehledes XML-Kommentar für öffentlich sichtbaren Typ oder Element