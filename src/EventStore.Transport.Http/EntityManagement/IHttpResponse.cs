using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace EventStore.Transport.Http.EntityManagement {
	public interface IHttpResponse {
		void AddHeader(string name, string value);
		void Close();
		long ContentLength64 { get; set; }
		string ContentType { get; set; }
		Stream OutputStream { get; }
		int StatusCode { get; set; }
		string StatusDescription { get; set; }
	}
}
