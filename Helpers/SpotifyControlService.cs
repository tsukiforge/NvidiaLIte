using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace NvidiaCi
{
    public sealed class SpotifyControlService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        private string? _accessToken;

        public bool HasAccessToken => !string.IsNullOrWhiteSpace(_accessToken);

        public void SetAccessToken(string accessToken)
        {
            _accessToken = string.IsNullOrWhiteSpace(accessToken) ? null : accessToken.Trim();
        }

        public Task PlayAsync() => SendCommandAsync(HttpMethod.Put, "me/player/play");
        public Task PauseAsync() => SendCommandAsync(HttpMethod.Put, "me/player/pause");
        public Task NextAsync() => SendCommandAsync(HttpMethod.Post, "me/player/next");

        private async Task SendCommandAsync(HttpMethod method, string endpoint)
        {
            if (!HasAccessToken)
                throw new InvalidOperationException("Masukkan Spotify Web API access token terlebih dahulu.");

            using var request = new HttpRequestMessage(method, $"https://api.spotify.com/v1/{endpoint}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            using HttpResponseMessage response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
                return;

            string message = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Token Spotify tidak valid atau sudah kedaluwarsa.",
                HttpStatusCode.Forbidden => "Spotify menolak kontrol ini. Periksa scope user-modify-playback-state dan status Premium.",
                HttpStatusCode.NotFound => "Tidak ada perangkat Spotify aktif. Buka Spotify dan mulai putar lagu terlebih dahulu.",
                (HttpStatusCode)429 => "Spotify membatasi permintaan. Coba lagi sebentar.",
                _ => $"Spotify API gagal dengan status {(int)response.StatusCode}."
            };
            throw new HttpRequestException(message);
        }
    }
}
