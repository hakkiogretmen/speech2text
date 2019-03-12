using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace FunctionApp2
{

    public class speechResult
    {

  
            [JsonProperty("Duration")]
            public long Duration;
            [JsonProperty("NBest")]
            public IList<NBest> NBest;
            [JsonProperty("Offset")]
            public int Offset;
            [JsonProperty("RecognitionStatus")]
            public string RecognitionStatus;
 

    }

    public class NBest
    {
        [JsonProperty("Confidence")]
        public float Confidence;
        [JsonProperty("Display")]
        public string Display;
        [JsonProperty("ITN")]
        public string ITN;
        [JsonProperty("Lexical")]
        public string Lexical;
        [JsonProperty("MaskedITN")]
        public string MaskedITN;

    }


}
