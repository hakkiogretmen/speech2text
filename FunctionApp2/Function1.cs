using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;
using System.Threading;
using System.Collections.Generic;
using System;
using Microsoft.WindowsAzure.Storage.RetryPolicies;

namespace FunctionApp2
{
    public static class Function1
    {
        private static List<string> _lines;

        [FunctionName("Function1")]
        public async static Task Run([BlobTrigger("audiostorage/{name}", Connection = "AzureWebJobsStorage")]CloudBlockBlob myBlob, string name, ILogger log,
                [Blob("cpxoutput/{name}", Connection = "AzureWebJobsStorage")] CloudBlockBlob outputBlob,
                [CosmosDB(
                databaseName: "cpxspeechdb",
                collectionName: "jsonresults",
                CreateIfNotExists = true,
                ConnectionStringSetting = "CosmosDBConnection")]
                DocumentClient jsonresults)
        {
            
            string SubscriptionKey = "b92036a0d1aa4093ab84ad1ca4d3263a";
            string ServiceRegion = "westeurope";
            int maxRetryCount = 3;
            var language = "en-US";
            var stopRecognition = new TaskCompletionSource<int>();

            // Creates an instance of a speech config with specified subscription key and service region.
            // Replace with your own subscription key and service region (e.g., "westus").
            var config = SpeechConfig.FromSubscription(SubscriptionKey, ServiceRegion);
  
            config.SpeechRecognitionLanguage = language;
            config.OutputFormat = OutputFormat.Detailed;
            // Replace with the CRIS endpoint id of your customized model.
            //config.EndpointId = "YourEndpointId";
                       
            await outputBlob.UploadTextAsync(JsonConvert.SerializeObject(myBlob.Properties)).ConfigureAwait(false);

            // Create a push stream
            using (var pushStream = AudioInputStream.CreatePushStream())
            {

            // Creates a speech recognizer using file as audio input.
            // Replace with your own audio file name.
                using (var audioInput = AudioConfig.FromWavFileInput(myBlob.StorageUri.PrimaryUri.LocalPath))
                {

                    log.LogInformation("#######################START#########################");
                    //log.LogInformation(audioInput.ToString());
                    using (var recognizer = new SpeechRecognizer(config, audioInput))
                    {

                        // Subscribes to events.
                        recognizer.Recognizing += (s, e) =>
                        {
                            log.LogInformation($"RECOGNIZING: Text={e.Result.Text}");
                        };

                        recognizer.Recognized += (s, e) =>
                        {
                            if (e.Result.Reason == ResultReason.RecognizedSpeech)
                            {
                                log.LogInformation($"RECOGNIZED: Text={e.Result.Text}");
                                string jsonstr = JsonConvert.SerializeObject(e.Result.ToString().Substring(e.Result.ToString().IndexOf('{')));
                                log.LogInformation($"Recognized Json={jsonstr}");

                                jsonstr = jsonstr.TrimStart('\"');
                                jsonstr = jsonstr.TrimEnd('\"');
                                jsonstr = jsonstr.Replace("\\", "");

                                try
                                {
                                    speechResult sr = JsonConvert.DeserializeObject<speechResult>(jsonstr);
                                    jsonresults.CreateDocumentAsync("dbs/cpxspeechdb/colls/jsonresults", sr).ConfigureAwait(false);

                                }
                                catch (Exception)
                                {

                                    throw;
                                }


                            }
                            else if (e.Result.Reason == ResultReason.NoMatch)
                            {
                                log.LogInformation($"NOMATCH: Speech could not be recognized.");
                            }
                        };

                        recognizer.Canceled += (s, e) =>
                        {
                            log.LogInformation($"CANCELED: Reason={e.Reason}");

                            if (e.Reason == CancellationReason.Error)
                            {
                                log.LogInformation($"CANCELED: ErrorCode={e.ErrorCode}");
                                log.LogInformation($"CANCELED: ErrorDetails={e.ErrorDetails}");
                                log.LogInformation($"CANCELED: Did you update the subscription info?");
                            }

                            stopRecognition.TrySetResult(0);
                        };

                        recognizer.SessionStarted += (s, e) =>
                        {
                            log.LogInformation("\n    Session started event.");
                        };

                        recognizer.SessionStopped += (s, e) =>
                        {
                            log.LogInformation("\n    Session stopped event.");
                            log.LogInformation("\nStop recognition.");
                            stopRecognition.TrySetResult(0);
                        };

                        // Starts continuous recognition. Uses StopContinuousRecognitionAsync() to stop recognition.
                        await recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);

                        using (MemoryStream ms = new MemoryStream())
                        {
                            var blobRequestOptions = new BlobRequestOptions
                            {
                                ServerTimeout = TimeSpan.FromSeconds(30),
                                MaximumExecutionTime = TimeSpan.FromSeconds(120),
                                RetryPolicy = new ExponentialRetry(TimeSpan.FromSeconds(3), maxRetryCount),
                            };

                            log.LogInformation("\n    Downloading blob to Stream async");
                            //CancellationToken ct = new CancellationToken();
                            myBlob.DownloadToStreamAsync(ms).Wait();
                            pushStream.Write(ms.ToArray());

                            //outputBlob.UploadFromStreamAsync(ms).Wait();
                        };

                        pushStream.Close();
                        
                        

                        // Waits for completion.
                        // Use Task.WaitAny to keep the task rooted.
                        Task.WaitAny(new[] { stopRecognition.Task });

                        // Stops recognition.
                        await recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);


                    }
                }
            }
            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{name}");
        }
    }
}
