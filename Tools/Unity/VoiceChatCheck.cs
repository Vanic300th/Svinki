using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class VoiceChatCheck
{
 private static void Assert(bool valid,string reason){if(!valid)throw new Exception(reason);}
 public static string Codec()
 {
  var signal=new float[VoiceCodec.FrameSamples];var decoded=new float[VoiceCodec.FrameSamples];int step=0;double energy=0,error=0;
  for(int frame=0;frame<100;frame++)
  {
   for(int i=0;i<signal.Length;i++){float t=(frame*signal.Length+i)/(float)VoiceCodec.SampleRate;signal[i]=.3f*Mathf.Sin(2*Mathf.PI*500*t)+.1f*Mathf.Sin(2*Mathf.PI*1700*t);}
   var packet=VoiceCodec.Encode(signal,ref step);Assert(packet.Length==164&&VoiceCodec.Decode(packet,decoded),"Frame size or decoding failed");
   if(frame==0)continue;for(int i=0;i<signal.Length;i++){energy+=signal[i]*signal[i];double d=signal[i]-decoded[i];error+=d*d;Assert(float.IsFinite(decoded[i])&&Mathf.Abs(decoded[i])<=1,"Invalid decoded sample");}
  }
  var stereo=new float[960*2];for(int i=0;i<960;i++){stereo[i*2]=.3f*Mathf.Sin(2*Mathf.PI*500*i/48000);stereo[i*2+1]=stereo[i*2];}
  VoiceCodec.DownmixResample(stereo,2,decoded);Assert(decoded.All(float.IsFinite)&&decoded.Max()>.29f&&decoded.Min()<-.29f,"Stereo downmix and native-rate conversion failed");
  double snr=10*Math.Log10(energy/error);Assert(snr>20,"Voice quality below 20 dB SNR");
  Assert(!VoiceCodec.IsValid(new byte[3]),"Malformed length accepted");var invalid=new byte[164];invalid[2]=89;Assert(!VoiceCodec.IsValid(invalid),"Invalid ADPCM step accepted");
  Assert(VoiceCodec.IsNewer(0,65535)&&!VoiceCodec.IsNewer(3,3)&&!VoiceCodec.IsNewer(2,3),"Sequence wrapping, duplicate or ordering broken");
  Assert(VoicePlayback.GainAtDistance(0)==1&&VoicePlayback.GainAtDistance(2)==1&&Mathf.Abs(VoicePlayback.GainAtDistance(13.5f)-.5f)<.001f&&VoicePlayback.GainAtDistance(25)==0&&VoicePlayback.GainAtDistance(50)==0,"Distance attenuation broken");
  Directory.CreateDirectory("ArtSource/VoiceChat");string result="PASS: 100 independently decodable ADPCM packets; SNR "+snr.ToString("F1")+" dB; 48 kHz stereo capture downmixed and resampled; malformed packets rejected; duplicate/reorder/wrap checks; gain full at 2 m, half at 13.5 m, silent from 25 m\n";
  File.WriteAllText("ArtSource/VoiceChat/codec-validation.txt",result);return result;
 }
 public static string Offline(){SessionState.SetString("Svinki.VoiceChat.History","RUNNING");PlayerRegistry.Players.First(p=>p.IsLocal).StartCoroutine(History());return "Persistent side history check running";}
 private static IEnumerator History()
 {
  foreach(var brain in UnityEngine.Object.FindObjectsByType<MannequinBrain>())brain.enabled=false;
  foreach(var thief in UnityEngine.Object.FindObjectsByType<ThiefBrain>())thief.enabled=false;
  var player=PlayerRegistry.Players.First(p=>p.IsLocal);var history=PlayerChat.Instance.GetComponent<PlayerChatHistory>();
  try
  {
   Assert(!ProximityVoice.CanTransmit(null)&&!ProximityVoice.Instance.IsTransmitting,"Microphone started outside online push to talk");
   for(int i=0;i<54;i++)PlayerChatBubble.Show(player.gameObject,"Message "+i+": this message remains in the side history.");
   Assert(history.Count==50&&history.Messages[0].Contains("Message 4:"),"History capacity or oldest eviction incorrect");
   PlayerChatBubble.Show(player.gameObject,"<size=100>literal message</size>");
   Assert(history.Messages.Last().Contains("<size=100>"),"Markup not kept literal");
  }catch(Exception e){Fail(e);yield break;}
  yield return new WaitForSecondsRealtime(6.3f);
  try
  {
   Assert(history.Count==50,"History expired with bubble");Assert(!player.GetComponent<PlayerChatBubble>().GetComponentsInChildren<Canvas>(true).Any(c=>c.gameObject.activeSelf),"Bubble did not expire");
   Assert(history.GetComponentsInChildren<TMPro.TMP_Text>().Where(t=>t.name=="Chat message").All(t=>!t.richText),"History accepts rich text markup");
   ScreenCapture.CaptureScreenshot(Path.GetFullPath("ArtSource/VoiceChat/side-history.png"));
   File.WriteAllText("ArtSource/VoiceChat/history-validation.txt","PASS: 50 persistent messages; oldest evicted; literal markup; history survives bubble timeout; microphone inactive offline\n");SessionState.SetString("Svinki.VoiceChat.History","PASS");
  }catch(Exception e){Fail(e);}
 }
 private static void Fail(Exception e){SessionState.SetString("Svinki.VoiceChat.History","FAIL "+e.Message);Debug.LogException(e);}
}
