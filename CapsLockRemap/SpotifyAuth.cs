using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CapsLockRemap
{

    static class SpotifyAuth
    {
        static Config config;

        private static string clientId = "";
        private static string clientSecret = "";
        private static string redirectUri = "http://localhost:5555/callback";

        private static string accessToken;
        private static string refreshToken;
        private static int expiresIn;
        private static Timer refreshTimer;

        static public async Task Init()
        {
            config = ConfigManager.Load();
            clientId = config.ClientId;
            clientSecret = config.ClientSecret;
            redirectUri = config.RedirectUri;

            if (config.EnableAPI)
            {
                await Authenticate();
                ScheduleRefresh();
            }
        }

        static async Task Authenticate()
        {
            string authUrl =
                "https://accounts.spotify.com/authorize" +
                "?response_type=code" +
                $"&client_id={clientId}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&scope=user-read-playback-state user-modify-playback-state user-library-modify";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = authUrl,
                UseShellExecute = true
            });


            var http = new HttpListener();
            http.Prefixes.Add("http://localhost:5555/callback/");
            http.Start();

            Console.WriteLine("Waiting for Spotify login...");
            var context = await http.GetContextAsync();
            string code = context.Request.QueryString["code"];

            string responseHtml = @"
            <html>
                <head><title>Spotify Auth</title></head>
                <body>
                <h2>Spotify authentication successful! This window will close automatically.</h2>
                <script>
                    window.onload = function() {
                        window.open('', '_self', '');
                        window.close();
                    };
                </script>
                </body>
            </html>";

            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.OutputStream.Close();
            http.Stop();

            await ExchangeCodeForTokens(code);
        }

        static async Task ExchangeCodeForTokens(string code)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri),
                });

                var response = await client.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                accessToken = root.GetProperty("access_token").GetString();
                refreshToken = root.GetProperty("refresh_token").GetString();
                expiresIn = root.GetProperty("expires_in").GetInt32();

                Console.WriteLine("Access token: " + accessToken);
                Console.WriteLine("Refresh token: " + refreshToken);
                Console.WriteLine("Expires in: " + expiresIn + " seconds");
            }
        }


        static void ScheduleRefresh()
        {
            int refreshDelay = (int)(expiresIn * 0.9) * 1000;
            refreshTimer = new Timer(async _ => await RefreshAccessToken(), null, refreshDelay, Timeout.Infinite);
            Console.WriteLine($"Scheduled token refresh in {refreshDelay / 1000} seconds");
        }

        static async Task RefreshAccessToken()
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
                string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

                request.Content = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", refreshToken),
            });

                var response = await client.SendAsync(request);
                string json = await response.Content.ReadAsStringAsync();

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                accessToken = root.GetProperty("access_token").GetString();
                expiresIn = root.GetProperty("expires_in").GetInt32();

                Console.WriteLine("Refreshed access token: " + accessToken);
                Console.WriteLine("New expiry: " + expiresIn + " seconds");

                ScheduleRefresh();
            }
        }


        public static async Task LikeCurrentSong()
        {
            if (!config.EnableAPI) return;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await client.GetAsync("https://api.spotify.com/v1/me/player/currently-playing");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("❌ Error getting currently playing track: " + response.StatusCode);
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("❌ No track is currently playing.");
                    return;
                }

                var obj = JsonDocument.Parse(json).RootElement;
                var track = obj.GetProperty("item");

                if (!track.TryGetProperty("id", out JsonElement trackIdElement))
                {
                    Console.WriteLine("❌ No track info found.");
                    return;
                }

                string trackId = track.GetProperty("id").GetString();
                string songName = track.GetProperty("name").GetString();
                string artist = track.GetProperty("artists")[0].GetProperty("name").GetString();

                Console.WriteLine($"🎵 Currently playing: {songName} - {artist}");

                // 2️⃣ Like the track
                var likeResponse = await client.PutAsync(
                    $"https://api.spotify.com/v1/me/tracks?ids={trackId}",
                    null
                );

                if (likeResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❤️ Liked {songName} by {artist}");
                }
                else
                {
                    Console.WriteLine("❌ Failed to like track: " + likeResponse.StatusCode);
                    string error = await likeResponse.Content.ReadAsStringAsync();
                    Console.WriteLine(error);
                }
            }
        }

    }

}
