﻿using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using MatrixSDK.Exceptions;
namespace MatrixSDK.Backends
{
	public class HttpBackend : IMatrixAPIBackend
	{
		private string baseurl;
		private string access_token;
		private HttpClient client;

		public HttpBackend(string apiurl){
			baseurl = apiurl;
			if (baseurl.EndsWith ("/")) {
				baseurl = baseurl.Substring (0, baseurl.Length - 1);
			}
			ServicePointManager.ServerCertificateValidationCallback += acceptCertificate;
			client = new HttpClient ();
		}

		private bool acceptCertificate (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors){
			return true;//Find a better way to handle mono certs.
		}

		public void SetAccessToken(string token){
			access_token = token;
		}

		private void getPath(ref string apiPath, bool auth){
			apiPath = baseurl + apiPath;
			if(auth){
				apiPath	+= (apiPath.Contains ("?") ? "&" : "?") + "access_token=" + access_token;
			}
		}

		private MatrixRequestError requestWrap (Task<HttpResponseMessage> task, out JObject result){
			try
			{
				HttpStatusCode code = GenericRequest (task, out result);
				return new MatrixRequestError ("", MatrixErrorCode.CL_NONE, code);
			}
			catch(MatrixServerError e){
				result = null;
				return new MatrixRequestError (e.ErrorCodeStr, e.ErrorCode, HttpStatusCode.OK);
			}
		}

		public MatrixRequestError Get  (string apiPath, bool authenticate, out JObject result){
			getPath (ref apiPath,authenticate);
			Task<HttpResponseMessage> task = client.GetAsync (apiPath);
			return requestWrap (task, out result);
		}

		public MatrixRequestError Put(string apiPath, bool authenticate, JObject data, out JObject result){
			StringContent content = new StringContent (data.ToString (), Encoding.UTF8, "application/json");
			getPath (ref apiPath,authenticate);
			Task<HttpResponseMessage> task = client.PutAsync(apiPath,content);
			return requestWrap (task, out result);
		}

		public MatrixRequestError Post(string apiPath, bool authenticate, JObject data, out JObject result){
			StringContent content;
			if (data != null) {
				content = new StringContent (data.ToString (), Encoding.UTF8, "application/json");
			} else {
				content = new StringContent ("{}");
			}
			getPath (ref apiPath,authenticate);
			Task<HttpResponseMessage> task = client.PostAsync(apiPath,content);
			return requestWrap (task, out result);
		}

		private HttpStatusCode GenericRequest(Task<HttpResponseMessage> task, out JObject result){
			Task<string> stask = null;
			result = null;
			try
			{
				task.Wait();
				if (task.Status == TaskStatus.RanToCompletion ) {
					stask = task.Result.Content.ReadAsStringAsync();
					stask.Wait();
				}
				else
				{
					return task.Result.StatusCode;
				}
			}
			catch(WebException e){
				throw e;
			}
			catch(AggregateException e){
				throw new MatrixException (e.InnerException.Message,e.InnerException);
			}
			if (stask.Status == TaskStatus.RanToCompletion) {
				try
				{
					result = JObject.Parse (stask.Result);
					if (result ["errcode"] != null) {
						throw new MatrixServerError (result ["errcode"].ToObject<string> (), result ["error"].ToObject<string> ());
					}
				}
				catch(JsonException e){
					//Regular web failure then
				}
			}
			return task.Result.StatusCode;

		}
	}
}

