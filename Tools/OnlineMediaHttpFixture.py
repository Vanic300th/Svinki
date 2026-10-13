"""Loopback-only ranged HTTP fixture for real Unity VideoPlayer regression; not game code."""
import http.server, functools, os, re, sys
class QuietHandler(http.server.SimpleHTTPRequestHandler):
 def log_message(self,*args):pass
 def send_head(self):
  self.remaining=None
  path=self.translate_path(self.path)
  if self.headers.get('Range') and os.path.isfile(path):
   size=os.path.getsize(path);m=re.match(r'bytes=(\d+)-(\d*)',self.headers['Range'])
   if m:
    start=int(m[1]);end=min(size-1,int(m[2]) if m[2] else size-1)
    if start>=size:self.send_error(416);return None
    f=open(path,'rb');f.seek(start);self.remaining=end-start+1
    self.send_response(206);self.send_header('Content-Type',self.guess_type(path));self.send_header('Accept-Ranges','bytes')
    self.send_header('Content-Range',f'bytes {start}-{end}/{size}');self.send_header('Content-Length',str(self.remaining));self.end_headers();return f
  return super().send_head()
 def copyfile(self,source,output):
  if self.remaining is None:return super().copyfile(source,output)
  while self.remaining>0:
   part=source.read(min(65536,self.remaining))
   if not part:break
   output.write(part);self.remaining-=len(part)
server=http.server.ThreadingHTTPServer(('127.0.0.1',8874),functools.partial(QuietHandler,directory=sys.argv[1]))
try: server.serve_forever()
finally: server.server_close()
