﻿using FilterPolishUtil;
using System.Collections.Generic;
using System.Linq;

namespace FilterCore.FilterComponents.Tags
{
    public class TierTag
    {
        public List<string> Tags { get; set; } = new List<string>();
        public string PrimaryTag => Tags.FirstOrDefault();

        public string CombinedTagValue =>
            StringWork.CombinePieces("->", Tags.Skip(1).ToList());

        public TierTag(params string[] tags)
        {
            this.Tags = new List<string>();
            this.Tags.AddRange(tags);
        }

        public string Serialize()
        {
            return $"${StringWork.CombinePieces("->", this.Tags)}";
        }
    }
}
