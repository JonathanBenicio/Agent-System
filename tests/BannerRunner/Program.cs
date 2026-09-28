using System;
using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.Fonts;
using System.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        var cleanImagePath = "clean_real-house.jpg";
        if (!File.Exists(cleanImagePath)) {
            Console.WriteLine("Arquivo limpo não encontrado: " + cleanImagePath);
            return;
        }

        var bannerPath = "banner_" + cleanImagePath;
        decimal price = 400000;
        int bedrooms = 3;
        string location = "Jardim São Paulo";
        string phone = "15 991903262";

        Console.WriteLine("Executando RenderBannerAsync (Versão ADM)...");
        using var image = await Image.LoadAsync(cleanImagePath);

        Font font;
        if (SystemFonts.TryGet("Arial", out var fontFamily))
            font = fontFamily.CreateFont(36, FontStyle.Bold);
        else if (SystemFonts.Families.Any())
            font = SystemFonts.Families.First().CreateFont(36, FontStyle.Bold);
        else return;

        Font bigFont = font.Family.CreateFont(56, FontStyle.Bold);
        var darkBlue = Color.ParseHex("#0B2046");
        var green = Color.ParseHex("#128A81");

        // --- 1. Tarja Superior Direita ---
        var topStripHeight = 80f;
        var topStripWidth = image.Width * 0.7f;
        var topStripLeft = image.Width - topStripWidth;

        var topPath = new PathBuilder()
            .AddLines(new PointF[] {
                new PointF(topStripLeft, 0),
                new PointF(image.Width, 0),
                new PointF(image.Width, topStripHeight),
                new PointF(topStripLeft + 30f, topStripHeight),
                new PointF(topStripLeft, 0)
            })
            .Build();

        image.Mutate(x => x.Fill(darkBlue, topPath));
        image.Mutate(x => x.DrawText($"VENDE-SE      (WHATSAPP) {phone}", font, Color.White, new PointF(topStripLeft + 60f, 15f)));

        // --- 2. Fita Base (Branca) ---
        var baseStripHeight = 100f;
        var baseRect = new RectangleF(0, image.Height - baseStripHeight, image.Width, baseStripHeight);
        image.Mutate(x => x.Fill(Color.White, baseRect));

        Font smallFont = font.Family.CreateFont(18, FontStyle.Bold);
        image.Mutate(x => x.DrawText("📍 Excelente Localizacao   🛡️ Bairro Tranquilo e Valorizado   🔑 Pronta para Morar   📄 Documentacao Regular", smallFont, darkBlue, new PointF(40, image.Height - 65f)));

        // --- 3. Tarjas de Valor e Localização ---
        var locPathHeight = 70f;
        var locPathWidth = 400f;
        var locPathY = image.Height - baseStripHeight - locPathHeight;

        var locPath = new PathBuilder()
            .AddLines(new PointF[] {
                new PointF(0, locPathY),
                new PointF(locPathWidth, locPathY),
                new PointF(locPathWidth - 30f, locPathY + locPathHeight),
                new PointF(0, locPathY + locPathHeight),
                new PointF(0, locPathY)
            })
            .Build();
        image.Mutate(x => x.Fill(darkBlue, locPath));
        image.Mutate(x => x.DrawText($"📍 {location.ToUpper()}", font, Color.White, new PointF(20f, locPathY + 15f)));

        var pricePathWidth = 500f;
        var pricePathHeight = 85f;
        var pricePathLeft = locPathWidth - 80f;
        var pricePathY = image.Height - baseStripHeight - pricePathHeight;

        var pricePath = new PathBuilder()
            .AddLines(new PointF[] {
                new PointF(pricePathLeft, pricePathY),
                new PointF(pricePathLeft + pricePathWidth, pricePathY),
                new PointF(pricePathLeft + pricePathWidth - 40f, pricePathY + pricePathHeight),
                new PointF(pricePathLeft - 40f, pricePathY + pricePathHeight),
                new PointF(pricePathLeft, pricePathY)
            })
            .Build();

        image.Mutate(x => x.Fill(green, pricePath));
        image.Mutate(x => x.DrawText($"R$ {price:N0}   |  {bedrooms} Qtos", bigFont, Color.White, new PointF(pricePathLeft + 20f, pricePathY + 10f)));

        await image.SaveAsync(bannerPath);
        Console.WriteLine("Imagens atualizadas com o design V2 de Alto Padrão concluído com sucesso!");
    }
}
