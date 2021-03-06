﻿using System;

namespace GuildWars2.ArenaNet.API.V2
{
    public class QuaggansRequest : Request<string, QuaggansResponse>
    {
        protected override string APIPath
        {
            get { return "/" + Version + "/quaggans"; }
        }

        public QuaggansRequest(params string[] quaggan_ids)
            : base(quaggan_ids)
        { }
    }
}
