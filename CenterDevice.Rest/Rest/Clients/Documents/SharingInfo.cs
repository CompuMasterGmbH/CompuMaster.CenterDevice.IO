﻿using RestSharp.Deserializers;
using System.Collections.Generic;

namespace CenterDevice.Rest.Clients.Documents
{
    public class SharingInfo
    {
        public List<string> Visible { get; set; }

        [DeserializeAs(Name = "not-visible-count")]
        public int NotVisibleCount { get; set; }

        public bool HasSharing
        {
            get
            {
                return NotVisibleCount != 0 || (Visible != null && Visible.Count != 0);
            }
        }
    }
}
