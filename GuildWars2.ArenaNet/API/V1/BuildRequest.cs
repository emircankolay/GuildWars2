﻿using System;

namespace GuildWars2.ArenaNet.API.V1
{
    public class BuildRequest : Request<BuildResponse>
    {
        protected override string APIPath
        {
            get { return "/" + Version + "/build.json"; }
        }
    }
}
