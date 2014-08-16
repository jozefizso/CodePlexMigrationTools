
namespace UserEcho
{
    using System;
    using RestSharp;

    /// <summary>
    /// Provides a UserEcho REST client. Based on https://github.com/UserEcho/userecho-api-csharp
    /// </summary>
    public class Client
    {
        public const string CLIENT_VERSION = "1.0";
        private const string ApiUrl = "https://userecho.com/api/v2/";
        private readonly string apiToken;
        private readonly RestClient client;

        public Client(string apiToken)
        {
            this.apiToken = apiToken;
            this.client = new RestClient(ApiUrl);
        }

        public object Request(Method method, string path, object parameters = null)
        {
            var request = new RestRequest(path + ".json", method);

            request.AddHeader("Authorization", "Bearer " + this.apiToken);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("API-Client", string.Format("userecho-csharp-{0}", CLIENT_VERSION));

            if (parameters != null)
            {
                request.AddParameter("application/json", SimpleJson.SerializeObject(parameters), ParameterType.RequestBody);
            }

            var response = this.client.Execute(request);

            try
            {
                return SimpleJson.DeserializeObject(response.Content);
            }
            catch (Exception exception)
            {
                throw new UEAPIError(response.Content, exception);
            }
        }

        public class UEAPIError : Exception { public UEAPIError(string msg, Exception innerException) : base(msg, innerException) { } }
        public object Get(string path) { return Request(Method.GET, path); }
        public object Post(string path, object parameters) { return Request(Method.POST, path, parameters); }
        public object Put(string path, object parameters) { return Request(Method.PUT, path, parameters); }
        public object Delete(string path, object parameters) { return Request(Method.DELETE, path, parameters); }
    }
}