using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/// <summary>Serves one bundled page on loopback. No filesystem routes, uploaded code or proxying.</summary>
public sealed class YouTubePlayerPage : IDisposable
{
 private readonly TcpListener listener;
 private readonly CancellationTokenSource stop=new CancellationTokenSource();
 private readonly byte[] response;
 private readonly string route;
 public string Url {get;}
 public YouTubePlayerPage(string html)
 {
  route="/"+Guid.NewGuid().ToString("N");listener=new TcpListener(IPAddress.Loopback,0);listener.Start(4);
  int port=((IPEndPoint)listener.LocalEndpoint).Port;Url="http://127.0.0.1:"+port+route;
  var body=Encoding.UTF8.GetBytes(html);
  var header=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nReferrer-Policy: strict-origin-when-cross-origin\r\nX-Content-Type-Options: nosniff\r\n\r\n");
  response=new byte[header.Length+body.Length];Buffer.BlockCopy(header,0,response,0,header.Length);Buffer.BlockCopy(body,0,response,header.Length,body.Length);
  _=Serve();
 }
 private async Task Serve()
 {
  while(!stop.IsCancellationRequested)
  {
   TcpClient client;try{client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);}catch(Exception){break;}
   _=Respond(client);
  }
 }
 private async Task Respond(TcpClient client)
 {
  using(client)using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
  {
   deadline.CancelAfter(3000);
   try
   {
    var stream=client.GetStream();var buffer=new byte[8192];int length=0;
    while(length<buffer.Length){int n=await stream.ReadAsync(buffer,length,buffer.Length-length,deadline.Token).ConfigureAwait(false);if(n==0)return;length+=n;if(Encoding.ASCII.GetString(buffer,0,length).Contains("\r\n\r\n"))break;}
    var request=Encoding.ASCII.GetString(buffer,0,length);
    var host=new Uri(Url).Authority;
    if(!request.StartsWith("GET "+route+" HTTP/1.",StringComparison.Ordinal)||request.IndexOf("\r\nHost: "+host+"\r\n",StringComparison.OrdinalIgnoreCase)<0)return;
    await stream.WriteAsync(response,0,response.Length,deadline.Token).ConfigureAwait(false);
   }
   catch(Exception){/* Disconnected local browser or timeout; no external network is served. */}
  }
 }
 public void Dispose(){stop.Cancel();listener.Stop();stop.Dispose();}
 public static int AvailablePort(){var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();return port;}
}
