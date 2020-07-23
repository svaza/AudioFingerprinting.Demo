
using NAudio.Wave;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using SIPSorcery.SIP.App;
using SIPSorcery.Sys;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SipSorcery.Demo
{
    class Program
    {
        static string USERNAME = "5256939939";
        static string PASSWORD = "3xgkps9vy3pscbx2";
        static string DOMAIN = "gw1.sip.us";
        static int SIP_LISTEN_PORT = 5060;
        //static string DEFAULT_CALL_DESTINATION = "sip:tel:14156550001@" + DOMAIN;
        static string DEFAULT_CALL_DESTINATION = "sip:14156550001@" + DOMAIN;
        static SIPUserAgent userAgent;
        static SIPTransport sipTransport;
        private static readonly WaveFormat _waveFormat = new WaveFormat(8000, 16, 1);
        private static WaveFileWriter _waveFile = new WaveFileWriter("output.mp3", _waveFormat);

        static async Task Main(string[] args)
        {
            sipTransport = new SIPTransport();
            sipTransport.AddSIPChannel(new SIPUDPChannel(new IPEndPoint(IPAddress.Any, SIP_LISTEN_PORT)));

            //RegisterUserAgent();
            //EnableTraceLogs(sipTransport, true);
            //Thread.Sleep(10000);
            await DialNumber();
            Console.ReadLine();
            //regUserAgent.Stop();
        }

        static async Task DialNumber()
        {
            string fromHeader = (new SIPFromHeader(USERNAME, new SIPURI(USERNAME, DOMAIN, null), null)).ToString();
            
            SIPCallDescriptor callDescriptor = new SIPCallDescriptor(USERNAME, PASSWORD, DEFAULT_CALL_DESTINATION, fromHeader, null, null, null, null, SIPCallDirection.Out, SDP.SDP_MIME_CONTENTTYPE, null, null);
            callDescriptor.CallId = "12028883999";
            
            userAgent = new SIPUserAgent(sipTransport, null);
            userAgent.ClientCallTrying += (uac, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Trying: {resp.StatusCode} {resp.ReasonPhrase}.");
            userAgent.ClientCallRinging += (uac, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Ringing: {resp.StatusCode} {resp.ReasonPhrase}.");
            userAgent.ClientCallFailed += (uac, err, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Failed: {err}, Status code: {resp?.StatusCode}");
            userAgent.ClientCallAnswered += (uac, resp) => Console.WriteLine($"{uac.CallDescriptor.To} Answered: {resp.StatusCode} {resp.ReasonPhrase}.");
            userAgent.OnDtmfTone += (key, duration) => OnDtmfTone(userAgent, key, duration);
            userAgent.OnRtpEvent += (evt, hdr) => Console.WriteLine($"rtp event {evt.EventID}, duration {evt.Duration}, end of event {evt.EndOfEvent}, timestamp {hdr.Timestamp}, marker {hdr.MarkerBit}.");
            userAgent.OnCallHungup += OnHangup;
            
            var rtpSession = new RtpAVSession(
                new AudioOptions
                {
                    AudioSource = AudioSourcesEnum.CaptureDevice,
                    AudioCodecs = new List<SDPMediaFormatsEnum> { SDPMediaFormatsEnum.PCMU, SDPMediaFormatsEnum.PCMA }
                },
                null);
            rtpSession.OnRtpPacketReceived += OnRtpPacketReceived;
            
            var callResult = await userAgent.Call(callDescriptor, rtpSession);
            Console.WriteLine($"Call result {((callResult) ? "success" : "failure")}.");
            if (callResult)
            {
                Console.WriteLine("Enter digits one after another");
                for (int i = 0; i < 11; i++)
                {
                    var p = Console.ReadLine();
                    await userAgent.SendDtmf(byte.Parse(p));
                }
            }
            Console.WriteLine("Enter ?");
            Console.ReadLine();
            await userAgent.SendDtmf(35);

            Thread.Sleep(60000);
           
            userAgent.Hangup();
            _waveFile.Dispose();
            Console.WriteLine("Hangup");
        }

        private static void OnRtpPacketReceived(SDPMediaTypesEnum mediaType, RTPPacket rtpPacket)
        {
            if (mediaType == SDPMediaTypesEnum.audio)
            {
                var sample = rtpPacket.Payload;

                for (int index = 0; index < sample.Length; index++)
                {
                    if (rtpPacket.Header.PayloadType == (int)SDPMediaFormatsEnum.PCMA)
                    {
                        short pcm = NAudio.Codecs.ALawDecoder.ALawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                    else
                    {
                        short pcm = NAudio.Codecs.MuLawDecoder.MuLawToLinearSample(sample[index]);
                        byte[] pcmSample = new byte[] { (byte)(pcm & 0xFF), (byte)(pcm >> 8) };
                        _waveFile.Write(pcmSample, 0, 2);
                    }
                }
            }
        }

        private static void OnDtmfTone(SIPUserAgent ua, byte key, int duration)
        {
            string callID = ua.Dialogue.CallId;
            Console.WriteLine($"Call {callID} received DTMF tone {key}, duration {duration}ms.");
        }

        private static void OnHangup(SIPDialogue dialogue)
        {
            if (dialogue != null)
            {
                string callID = dialogue.CallId;
                userAgent.Close();
            }
        }
    }
}
