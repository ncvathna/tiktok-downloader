using System;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;

namespace TiktokDownloader
{
    // Only CleanUrl works; Watermark Url doesn't (403 Access Denied)
    class Program
    {
        static void Main(string[] args)
        {
            HttpClient client = new HttpClient();
            for (int i = 0; i < args.Length; i++)
            {
                // Handle each video url
                bool isVideoUrl = Regex.Match(args[i], @".+tiktok\.com/@[^/]+/video/[0-9]+$").Success;
                if (isVideoUrl)
                {
                    Video video = new Video(args[i], client);
                    Console.Write(video);
                    video.DownloadCleanUrl();
                }
            }
        }

        class Video
        {
            // See <script id="__NEXT_DATA__" type="application/json" crossorigin="anonymous"> in the video page for more attributes
            public string Username { private set; get; }
            public string WebId { private set; get; }
            public DateTime CreateTime { private set; get; }
            public string WatermarkUrl { private set; get; }
            public string CleanUrl { private set; get; }

            private int serverCode;
            private HttpClient client;
            public Video(string url, HttpClient client)
            {
                this.client = client;

                // Get Video Page Content
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/87.0.4280.88 Safari/537.36");
                HttpResponseMessage response = client.GetAsync(url).Result;
                string doc = response.Content.ReadAsStringAsync().Result;
                string metadata = doc.Split("<script id=\"__NEXT_DATA__\" type=\"application/json\" crossorigin=\"anonymous\">")[1].Split("</script>")[0];

                // Username
                Username = url.Split("/@")[1].Split("/")[0];
                // Web ID
                WebId = url.Split("/video/")[1];

                // Check if video exists
                serverCode = int.Parse(metadata.Split("\"serverCode\":")[1].Split(",")[0]);
                if (serverCode != 200) return;

                // CreateTime
                int unixTime = int.Parse(metadata.Split(",\"createTime\":")[1].Split(",")[0]);
                CreateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                CreateTime = CreateTime.AddSeconds(unixTime);
                // Thumbnail is at <meta property="og:image" content="https://p16-sign-sg.tiktokcdn.com/..."/>
                // Watermark Url
                WatermarkUrl = metadata.Split("\"downloadAddr\":\"")[1].Split("\",")[0].Replace("\\u0026", "&");
                // Clean Url
                string videoKey = metadata.Split("\"video\":{\"id\":\"")[1].Split("\",")[0];
                CleanUrl = "https://api2-16-h2.musical.ly/aweme/v1/play/?video_id=" + videoKey + "&vr_type=0&is_play_url=1&source=PackSourceEnum_PUBLISH&media_type=4";
            }

            public override string ToString()
            {
                if (serverCode != 200) return "\n==============================\n"
                        + "Username: " + Username + "\n\n"
                        + "Web Id: " + WebId + "\n\n"
                        + "Unable to retrieve video info.\n"
                        + "==============================\n\n";
                else return "\n==============================\n"
                    + "Username: " + Username + "\n\n"
                    + "Web Id: " + WebId + "\n\n"
                    + "Create Time: " + CreateTime + "\n\n"
                    + "Watermark Url: " + WatermarkUrl + "\n\n"
                    + "Clean Url: " + CleanUrl + "\n"
                    + "==============================\n\n";
            }

            public void DownloadCleanUrl() {
                string filename = Username + "-" + WebId + ".mp4";
                if (serverCode != 200)
                {
                    Console.WriteLine("- Unable to download " + filename + "!");
                    return;
                }

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "okhttp");
                client.DefaultRequestHeaders.Add("Referrer", "https://www.tiktok.com/");
                client.DefaultRequestHeaders.Add("Range", "bytes=0-");

                FileStream fs = File.Create(filename);
                client.GetAsync(CleanUrl).Result.Content.ReadAsStreamAsync().Result.CopyTo(fs);
                fs.Close();
                File.SetCreationTime(filename, CreateTime);
                File.SetLastWriteTime(filename, CreateTime);
                Console.WriteLine("+ Successfully Downloaded " + filename + "!");
            }
        }
    }
}
