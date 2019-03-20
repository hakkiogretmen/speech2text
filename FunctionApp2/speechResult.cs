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

    public class zoomMediaResultState
    {
        [JsonProperty("sessionId")]
        public String sessionId;
        [JsonProperty("done")]
        public Boolean done;
        [JsonProperty("results")]
        public List<zoomMediaResult> results = null;
        [JsonProperty("metadata")]
        public zoomMediaMetaData metadata;
    }

    public class zoomMediaMetaData
    {
        [JsonProperty("format")]
        public String format;
        [JsonProperty("filename")]
        public String filename;
        [JsonProperty("mimetype")]
        public String mimetype;
        [JsonProperty("duration")]
        public int duration;
    }

    public class zoomMediaResult
    {
        [JsonProperty("result")]
        public List<List<String>> result = null;
        [JsonProperty("text")]
        public String text;
        [JsonProperty("speaker")]
        public String speaker;
        [JsonProperty("sconf")]
        public float sconf;

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