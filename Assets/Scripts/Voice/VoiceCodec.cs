using System;

// Independently decodable IMA ADPCM frames keep packet loss from corrupting later speech.
public static class VoiceCodec
{
    public const int SampleRate = 16000, FrameSamples = 320, PacketBytes = 164;
    private static readonly int[] Index = { -1,-1,-1,-1,2,4,6,8 };
    private static readonly int[] Steps = { 7,8,9,10,11,12,13,14,16,17,19,21,23,25,28,31,34,37,41,45,50,55,60,66,73,80,88,97,107,118,130,143,157,173,190,209,230,253,279,307,337,371,408,449,494,544,598,658,724,796,876,963,1060,1166,1282,1411,1552,1707,1878,2066,2272,2499,2749,3024,3327,3660,4026,4428,4871,5358,5894,6484,7132,7845,8630,9493,10442,11487,12635,13899,15289,16818,18500,20350,22385,24623,27086,29794,32767 };
    private static int Clamp(int value,int min,int max)=>value<min?min:value>max?max:value;
    private static int Pcm(float sample)=>float.IsFinite(sample)?Clamp((int)(Math.Clamp(sample,-1f,1f)*32767),-32768,32767):0;
    public static bool IsValid(byte[] packet)=>packet!=null&&packet.Length==PacketBytes&&packet[2]<=88&&packet[3]==0;
    public static byte[] Encode(float[] samples,ref int stepIndex)
    {
        if(samples==null||samples.Length!=FrameSamples)throw new ArgumentException("Voice frame must contain 320 samples");
        stepIndex=Clamp(stepIndex,0,88);int predictor=Pcm(samples[0]);var bytes=new byte[PacketBytes];
        bytes[0]=(byte)predictor;bytes[1]=(byte)(predictor>>8);bytes[2]=(byte)stepIndex;
        for(int i=1;i<FrameSamples;i++)
        {
            int step=Steps[stepIndex],delta=Pcm(samples[i])-predictor,code=0;
            if(delta<0){code=8;delta=-delta;}
            int reconstructed=step>>3;
            if(delta>=step){code|=4;delta-=step;reconstructed+=step;}
            if(delta>=step>>1){code|=2;delta-=step>>1;reconstructed+=step>>1;}
            if(delta>=step>>2){code|=1;reconstructed+=step>>2;}
            predictor=Clamp(predictor+((code&8)!=0?-reconstructed:reconstructed),-32768,32767);
            stepIndex=Clamp(stepIndex+Index[code&7],0,88);
            int n=i-1;bytes[4+n/2]|=(byte)(code<<((n&1)*4));
        }
        return bytes;
    }
    public static bool Decode(byte[] bytes,float[] output)
    {
        if(!IsValid(bytes)||output==null||output.Length!=FrameSamples)return false;
        int predictor=(short)(bytes[0]|bytes[1]<<8),stepIndex=bytes[2];output[0]=predictor/32768f;
        for(int i=1;i<FrameSamples;i++)
        {
            int n=i-1,code=(bytes[4+n/2]>>((n&1)*4))&15,step=Steps[stepIndex];
            int delta=(step>>3)+((code&4)!=0?step:0)+((code&2)!=0?step>>1:0)+((code&1)!=0?step>>2:0);
            predictor=Clamp(predictor+((code&8)!=0?-delta:delta),-32768,32767);stepIndex=Clamp(stepIndex+Index[code&7],0,88);
            output[i]=predictor/32768f;
        }
        return true;
    }
    public static void DownmixResample(float[] input,int channels,float[] output)
    {
        if(input==null||output==null||channels<1||input.Length%channels!=0||input.Length==0||output.Length==0)throw new ArgumentException("Invalid capture buffer");
        int frames=input.Length/channels;float ratio=(float)frames/output.Length;
        for(int i=0;i<output.Length;i++)
        {
            float value=0;
            if(ratio>=1)
            {
                float start=i*ratio,end=(i+1)*ratio;
                for(int frame=(int)start;frame<Math.Min(frames,(int)Math.Ceiling(end));frame++)
                {
                    float weight=Math.Min(end,frame+1)-Math.Max(start,frame);
                    for(int c=0;c<channels;c++)value+=input[frame*channels+c]*weight;
                }
                value/=ratio*channels;
            }
            else
            {
                float position=Math.Clamp((i+.5f)*ratio-.5f,0,frames-1);int first=(int)position,last=Math.Min(first+1,frames-1);float t=position-first;
                for(int c=0;c<channels;c++)value+=input[first*channels+c]*(1-t)+input[last*channels+c]*t;
                value/=channels;
            }
            output[i]=float.IsFinite(value)?Math.Clamp(value,-1,1):0;
        }
    }
    public static bool IsNewer(ushort sequence,ushort previous)=>(short)(sequence-previous)>0;
}
