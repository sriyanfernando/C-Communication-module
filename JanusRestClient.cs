using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JanusSignalingIntegration
{
	public static class Commands
	{
		public const string CreateCommand = "create";
        public const string AttachCommand = "attach";
        public const string MessageCommand = "message";
        public const string TrickleCommand = "trickle";
        public const string DetachCommand = "detach";
		public const string EventCommand = "event"; // for long polling
		public const string KeepAliveCommand = "keepalive";
		
		
		public const string WebRtcUpCommand = "webrtcup";
        public const string MediaCommand = "media";
        public const string SlowLinkCommand = "slowlink";
        public const string HangUpCommand = "hangup";
        public const string DestroyCommand ="destroy";

		public const string SuccessCommand = "success";
		public const string AckCommand = "ack";

		// video room plugin commands
		public const string JoinedCommand = "joined";
		

	}

	public class Pubisher
	{
		public long id { get; set; }
		public string display { get; set; }
		public string audio_codec { get; set; }
		public string video_codec { get; set; }
		public bool talking { get; set; }
	}

	public class PluginDataContainer
	{
		public string videoroom { get; set; }
		public long room { get; set; }
		public string description { get; set; }
		public long id { get; set; }
		public long private_id { get; set; }


		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public List<Pubisher> publishers { get; set; }
	}

	public class PluginData
	{
		public string plugin { get; set; }

		public PluginDataContainer data { get; set; }
	}

	public class JanusJsonObject
	{
		public JanusJsonObject()
		{
			id = null;
		}

		/// <summary>
		/// Contains the command sent / received
		/// </summary>
		public string janus { get; set; }
		
        /// <summary>
		/// the handle identifier
		/// </summary>
        public long  sender { get; set; } 

        ///<summary>
        ///plugin
        ///</summary>
        public string plugin { get; set; }

		/// <summary>
		/// Transaction id
		/// </summary>
		public string transaction { get; set; }


        /// <summary>
		/// Unique identifier of the plugin
		/// </summary>
        public string handleId { get; set; }

		public Dictionary<string, string> data { get; set; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public PluginData plugindata { get; set; }

		/// <summary>
		/// Error data sent by janus
		/// </summary>
		public Dictionary<string, string> error { get; set; }

		/// <summary>
		/// Session identifier
		/// </summary>
		[JsonProperty(NullValueHandling=NullValueHandling.Ignore)]
		public long? id { get; set; }
		
		/// <summary>
		/// audio or video
		/// </summary>
        public string type { get; set; }
		
		/// <summary>
		/// audio or video reciving or not
		/// </summary>
        public bool receiving { get; set;}
		
		/// <summary>
		/// user sent many NACKs in the last second, uplink=true is from Janus' perspective
		/// </summary>
		public bool uplink { get; set; }
		
		/// <summary>
		/// number of NACKs in the last second
		/// </summary>
		public int nacks { get; set; }
		
		/// <summary>
		/// DTLS alert
		/// </summary>
		public string reason {get; set;}


        /// <summary>
		/// body send to janus
		/// </summary>
        public Dictionary<string, object> body { get; set; }

		/// <summary>
		/// jsep send from janus
		/// make this a seperate class
		/// </summary>
		[JsonProperty(NullValueHandling=NullValueHandling.Ignore)]
		public Dictionary<string, string> jsep { get ; set ; }

		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public Dictionary<string, object> candidate { get; set; }

    }

    public class JanusRestClient
	{   

	    public string Reason {get; set;}
	    public bool Uplink {get; set;}
		public int Nacks {get; set;}
        public string Type { get; set; }
        public bool Receiving {get; set; }
		public long SessionId;
		public long HandleId;
        public Dictionary<string, object> Candidate { get; set; }


        public string Ip { get; set; }
		public int Port { get; set; }

		private string Protocol = "https://";
		private string MediaType = "application/json";


		private string PluginName = "janus.plugin.videoroom";
		private long RoomId = 1234;
		private string RoomUserName = "Pradeep1";

		private BackgroundWorker _pollingWorker;

		public string JanusHost { get { return string.Format("{0}{1}:{2}/{3}", Protocol, Ip, Port, "janus/"); } }
		public string JanusHostSessionEndPoint { get { return string.Format("{0}{1}", JanusHost, SessionId); } }
		public string JanusHostSessionHandleEndPoint { get { return string.Format("{0}/{1}", JanusHostSessionEndPoint, HandleId); } }

		public event Action<JanusJsonObject> OnPollingMessageReceived;

        public JanusRestClient()
        {
			ServicePointManager.ServerCertificateValidationCallback += 	(sender, cert, chain, sslPolicyErrors) => true;
			SessionId = long.MinValue;
			_pollingWorker = new BackgroundWorker();
            _pollingWorker.WorkerSupportsCancellation = true;
            _pollingWorker.DoWork += _pollingWorker_DoWork;
            _pollingWorker.RunWorkerCompleted += _pollingWorker_RunWorkerCompleted;
            _pollingWorker.RunWorkerAsync();

        }

        ~JanusRestClient()
        {
            _pollingWorker.CancelAsync(); 
        }

		private void _pollingWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			while (!_pollingWorker.CancellationPending)
			{
				try
				{
					if (SessionId != long.MinValue)
					{
						JanusJsonObject jsonObj = null;
						var task = PollingEventMessage();
						task.Wait(6000);
						jsonObj = task.Result;

						if (jsonObj != null)
						{
							if (jsonObj.janus == Commands.KeepAliveCommand)
							{
								Console.WriteLine("Keep alive");
							}
							else
							{
								try
								{
									OnPollingMessageReceived(jsonObj);
								}
								catch (Exception ex)
								{
									Console.WriteLine(ex);
								}
							}
						}
					}
					Thread.Sleep(100);
				}
				catch (Exception ex)
				{


				}


			}
		}

        private void _pollingWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// start the session
        /// </summary>
        public async Task<JanusJsonObject> CreateSession()
		{
			JanusJsonObject retVal = null;
			try
			{
				var jsonObject = new JanusJsonObject()
				{
					janus = Commands.CreateCommand,
					transaction = Guid.NewGuid().ToString()
				};

				using (var httpClient = new HttpClient{Timeout=TimeSpan.FromSeconds(5000)})
				{
					var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
					var result = await httpClient.PostAsync(JanusHost, content);
					if (result.StatusCode == System.Net.HttpStatusCode.OK)
					{
						var body = await result.Content.ReadAsStringAsync();
						retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
                        long.TryParse(retVal.data["id"], out SessionId);
					}
				}
			}catch(Exception ex) { Console.WriteLine(ex); }
			return retVal;
		}
	  
	    ///<summary>
	    ///Starting point of Attaching session
	    ///</summary>
        public async Task<JanusJsonObject> AttachSession(){
            JanusJsonObject retVal = null;

            try
            {
                var jsonObject = new JanusJsonObject()
                {
                    janus = Commands.AttachCommand,
                    transaction = Guid.NewGuid().ToString(),
					plugin = PluginName,
                    //opaqueId = OpaqueId

                };

                using (var httpClient = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
                    var result = await httpClient.PostAsync(JanusHostSessionEndPoint, content);
                    if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var body = await result.Content.ReadAsStringAsync();
                        retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
						long.TryParse(retVal.data["id"], out HandleId);
					}
                }
                return retVal;

                
            }catch(Exception) {
                return retVal;
            }

        }

		public async Task<JanusJsonObject> PollingEventMessage()
		{
			JanusJsonObject retVal = null;
			try
			{
				var jsonObject = new JanusJsonObject()
				{
					janus = Commands.MessageCommand,
					sender = SessionId,
					transaction = Guid.NewGuid().ToString()
				};

				using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5000) })
				{
					var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
					var result = await httpClient.GetAsync(string.Format("{0}{1}", JanusHost, SessionId));
					if (result.StatusCode == System.Net.HttpStatusCode.OK)
					{
						var body = await result.Content.ReadAsStringAsync();
						retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
					}
				}
			}
			catch (Exception ex) { Console.WriteLine(ex); }
			return retVal;
		}


		public async Task<JanusJsonObject> RegisterRoom()
        {
            JanusJsonObject retVal = null;
			try
			{
				var jsonObject = new JanusJsonObject()
				{
					janus = Commands.MessageCommand,
					transaction = Guid.NewGuid().ToString(),
					body = new Dictionary<string, object> { { "request", "join" }, { "room", RoomId }, { "ptype", "publisher" }, { "display", RoomUserName } }
                };

                using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5000) })
                {
                    var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
                    var result = await httpClient.PostAsync(JanusHostSessionHandleEndPoint, content);
                    if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var body = await result.Content.ReadAsStringAsync();
                        retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
                    }
                }
            }
            catch (Exception) { }
            return retVal;
        }

		public async Task<JanusJsonObject> CreateOffer(string sdp, bool audio, bool video)
		{
			JanusJsonObject retVal = null;
			try
			{
				var jsonObject = new JanusJsonObject()
				{
					janus = Commands.MessageCommand,
					transaction = Guid.NewGuid().ToString(),
					body = new Dictionary<string, object> { { "request", "configure" }, { "audio", audio }, { "video", video } },
					jsep = new Dictionary<string, string> { { "type", "offer" }, { "sdp", sdp.ToString() } }
				};

				using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5000) })
				{
					var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
					var result = await httpClient.PostAsync(JanusHostSessionHandleEndPoint, content);
					if (result.StatusCode == System.Net.HttpStatusCode.OK)
					{
						var body = await result.Content.ReadAsStringAsync();
						retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
					}
				}
			}
			catch (Exception) { }
			return retVal;
		}

		public async Task<JanusJsonObject> SendCandidate(string sdpMid, int sdpMlineIndex, string sdp)
		{
			JanusJsonObject retVal = null;
			try
			{
				var jsonObject = new JanusJsonObject()
				{
					janus = Commands.TrickleCommand,
					transaction = Guid.NewGuid().ToString(),
					candidate = new Dictionary<string, object> { { "candidate", sdp }, { "sdpMLineIndex", sdpMlineIndex }, { "sdpMid", sdpMid } }
				};

				using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5000) })
				{
					var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
					var result = await httpClient.PostAsync(JanusHostSessionHandleEndPoint, content);
					if (result.StatusCode == System.Net.HttpStatusCode.OK)
					{
						var body = await result.Content.ReadAsStringAsync();
						retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
					}
				}
			}
			catch (Exception) { }
			return retVal;
		}

		///<summary>
		///Starting point of destroying session
		///</summary>
		public async Task<JanusJsonObject> DestroySession(){
            JanusJsonObject retVal = null;

            try
            {
                var jsonObject = new JanusJsonObject()
                {
                    janus = Commands.DestroyCommand,
                    transaction = Guid.NewGuid().ToString(),
					

                };

                using (var httpClient = new HttpClient())
                {
                    var content = new StringContent(JsonConvert.SerializeObject(jsonObject), Encoding.UTF8, MediaType);
                    var result = await httpClient.PostAsync(JanusHost, content);
                    if (result.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        var body = await result.Content.ReadAsStringAsync();
                        retVal = JsonConvert.DeserializeObject<JanusJsonObject>(body.ToString());
                    }
                }
                return retVal;
            }catch(Exception) {
                return retVal;
            }

        }
    }



}
