﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChallengeModeDatabase
{
    internal class DescriptionFile
    {
        public string description { get; set; }
        public long fileId { get; set; }
        public string localId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int version { get; set; }
    }
    internal class ChallengeList
    {
        public string[] challenges { get; set; }
    }
}
