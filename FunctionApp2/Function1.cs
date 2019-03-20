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
using System.Net.Http;
using System.Net.Http.Headers;

namespace FunctionApp2
{

    public static class Function1
    {
        private const string token = "21ce1a536aa3d5c3e39dee7805739244a3b72e23b2";
        static string SessionId { get; set; }

        private static async Task OpenSession(ILogger logger)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("X-Zoom-S2T-Key", token);
                StringContent stringContent = new StringContent("{\"language\":\"en-us\"}");
                stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage httpResponseMessage = await httpClient.PostAsync("https://api.zoommedia.ai/api/v1/speech-to-text/session", stringContent).ConfigureAwait(false);
                logger.LogInformation("open session response: " + httpResponseMessage);
                if (httpResponseMessage.IsSuccessStatusCode)
                {
                    string sessionString = await httpResponseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                    JToken token = JObject.Parse(sessionString);
                    SessionId = (string)token.SelectToken("sessionId");
                    string language = (string)token.SelectToken("language");
                    logger.LogInformation("session id: " + SessionId);
                    logger.LogInformation("language: " + language);
                }
                else
                {
                    string failureMsg = "HTTP Status: " + httpResponseMessage.StatusCode.ToString() + " - Reason: " + httpResponseMessage.ReasonPhrase;
                }
            }
        }

        
        private static async Task<string> UploadFileMultipart(string fileName, byte[] fileBytes, ILogger logger)
        {
            HttpContent bytesContent = new ByteArrayContent(fileBytes);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "multipart/form-data");
                client.DefaultRequestHeaders.Add("X-Zoom-S2T-Key", token);
                using (var formData = new MultipartFormDataContent())
                {

                    formData.Add(bytesContent, "upload", fileName);
                    logger.LogInformation("https://api.zoommedia.ai/api/v1/api/v1/speech-to-text/session/" + SessionId);
                    HttpResponseMessage response = await client.PostAsync(string.Format("https://api.zoommedia.ai/api/v1/speech-to-text/session/{0}", SessionId), formData).ConfigureAwait(false);
                    logger.LogInformation($"[VERBOSE]::Upload Response:{response}");
                    if (!response.IsSuccessStatusCode)
                    {
                        
                        logger.LogError($"[ERROR]::Error while getting response from zoommedia. Status Code: {response.StatusCode}");
                        return null;
                    }
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }
        
        //unused function
        private static async Task<string> UploadFileViaUrl(string fileName, string url, ILogger logger)
        {
            using (var httpClient = new HttpClient())
            {
                logger.LogInformation("file url: " + url);
                httpClient.DefaultRequestHeaders.Add("X-Zoom-S2T-Key", token);
                StringContent stringContent = new StringContent("{\"video_url\":\"https://cpxblob.blob.core.windows.net/audiostorage/f2bjrop10.wav\"}");
                stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var response = await httpClient.PostAsync(string.Format("https://api.zoommedia.ai/api/v1/api/v1/speech-to-text/session/{0}", SessionId), stringContent).ConfigureAwait(false);
                logger.LogInformation(response.ToString());
                if (response.IsSuccessStatusCode)
                {
                    string sessionString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    JToken token = JObject.Parse(sessionString);
                    SessionId = (string)token.SelectToken("sessionId");
                    logger.LogInformation(SessionId);
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
                return null;
            }
        }

        static async Task<string> GetResultAsync(DocumentClient jsonresults, ILogger log)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("X-Zoom-S2T-Key", token);
            client.DefaultRequestHeaders
              .Accept
              .Add(new MediaTypeWithQualityHeaderValue("application/json"));
            string actuallyDone = string.Empty;
            HttpResponseMessage response = new HttpResponseMessage();
            while (actuallyDone != "True")
            {
                response = await client.GetAsync(string.Format("https://api.zoommedia.ai/api/v1/speech-to-text/session/{0}", SessionId)).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string sessionString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    JToken token = JObject.Parse(sessionString);
                    actuallyDone = (string)token.SelectToken("done");
                    if (actuallyDone == "True")
                    {
                        break;
                    }
                    else
                    {
                        Thread.Sleep(10000);
                    }
                }
            }
            string Result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            try
            {
                zoomMediaResultState zrs = JsonConvert.DeserializeObject<zoomMediaResultState>(Result);
                await jsonresults.CreateDocumentAsync("dbs/cpxspeechdb/colls/jsonresults", zrs).ConfigureAwait(false);

            }
            catch (Exception e)
            {
                log.LogError(e, "[ERROR]::Error while inserting JSON into COSMOS");
                throw;
            }
            return Result;
        }
        private static async Task SaveMetaData(CloudBlockBlob myBlob, CloudBlockBlob outputBlob, ILogger logger)
        {
            try
            {
                await outputBlob.UploadTextAsync(JsonConvert.SerializeObject(myBlob.Properties)).ConfigureAwait(false);
                logger.LogInformation("[VERBOSE]::Uploading Metadata...");
            }
            catch (Exception e)
            {
                logger.LogError(e, "[ERROR]::Error occured while uploading metadata of the source audio file.");
            }
        }

        [FunctionName("Function1")]
        public async static Task Run([BlobTrigger("audiostorage/{name}", Connection = "AzureWebJobsStorage")]CloudBlockBlob myBlob, string name, ILogger log,
                [Blob("cpxoutput/{name}.json", Connection = "AzureWebJobsStorage")] CloudBlockBlob outputBlob,
                [CosmosDB(
                databaseName: "cpxspeechdb",
                collectionName: "jsonresults",
                CreateIfNotExists = true,
                ConnectionStringSetting = "CosmosDBConnection")]
                DocumentClient jsonresults)
        {

            long fileByteLength = myBlob.Properties.Length;
            byte[] fileBytes = new byte[fileByteLength];

            for (int i = 0; i < fileByteLength; i++)
            {
                fileBytes[i] = 0x20;
            }
            myBlob.DownloadToByteArrayAsync(fileBytes, 0).Wait();

            log.LogInformation("[VERBOSE]::Blob name: " + myBlob.Name);
            log.LogInformation("[VERBOSE]::Blob size: " + fileBytes.Length);

            await SaveMetaData(myBlob, outputBlob, log).ConfigureAwait(false);
            await OpenSession(log).ConfigureAwait(false);
            //await UploadFileViaUrl(myBlob.Name, myBlob.Uri.AbsoluteUri, log).ConfigureAwait(false);
            await UploadFileMultipart(myBlob.Name, fileBytes, log).ConfigureAwait(false);
            String resultJson = await GetResultAsync(jsonresults,log).ConfigureAwait(false);
            log.LogInformation(resultJson);

            log.LogInformation($"[VERBOSE]::Blob trigger function Processed blob\n Name:{name}");
        }
    }
}