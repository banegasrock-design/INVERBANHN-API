namespace InverbanHN.Shared.Options
{
    public class PixelPaySettings
    {
        public string BaseUrl { get; set; } = "https://pixelpay.app/api/v2";
        public string Key { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
    }
}
