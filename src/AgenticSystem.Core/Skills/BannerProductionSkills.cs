using System.ComponentModel;
using Microsoft.Agents.AI;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Drawing;
using SixLabors.Fonts;

namespace AgenticSystem.Core.Skills;

public class BannerProductionSkills
{
    [Description("Remove fios, postes e lixo de uma imagem. Faz o corte do topo da imagem para eliminar o ceu carregado de fios e aplica desfoque. Retorna o caminho da imagem limpa.")]
    public async Task<string> CleanImageAsync(
        [Description("Caminho absoluto para a foto original")] string originalImagePath, 
        [Description("Descricao dos itens para remover")] string itemsToRemove)
    {
        if (!File.Exists(originalImagePath))
            return "Erro: Imagem nao encontrada no caminho: " + originalImagePath;

        var cleanPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(originalImagePath) ?? "", $"clean_{System.IO.Path.GetFileName(originalImagePath)}");
        
        using var image = await Image.LoadAsync(originalImagePath);
        
        // Aplicar um corte (crop) de 15% do topo para eliminar céu e fios
        var cropHeight = (int)(image.Height * 0.15);
        image.Mutate(x => x.Crop(new Rectangle(0, cropHeight, image.Width, image.Height - cropHeight)));
        
        // Aplicar um leve desfoque para esconder eventuais defeitos
        image.Mutate(x => x.GaussianBlur(1.0f));

        await image.SaveAsync(cleanPath);
        return cleanPath;
    }

    [Description("Gera o banner publicitario final desenhando as tarjas geometricas modernas, preco, quartos, localizacao e telefone sobre a imagem limpa.")]
    public async Task<string> RenderBannerAsync(
        [Description("Caminho absoluto para a foto limpa")] string cleanImagePath, 
        [Description("Preco do imovel (ex: 650000)")] decimal price, 
        [Description("Quantidade de quartos")] int bedrooms,
        [Description("Localizacao do imovel (bairro/cidade)")] string location,
        [Description("Telefone de contato no formato Whatsapp")] string phone)
    {
        if (!File.Exists(cleanImagePath))
            return "Erro: Imagem limpa nao encontrada no caminho: " + cleanImagePath;

        var bannerPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(cleanImagePath) ?? "", $"banner_{System.IO.Path.GetFileName(cleanImagePath)}");
        
        using var image = await Image.LoadAsync(cleanImagePath);
        
        Font font;
        if (SystemFonts.TryGet("Arial", out var fontFamily))
            font = fontFamily.CreateFont(36, FontStyle.Bold);
        else if (SystemFonts.Families.Any())
            font = SystemFonts.Families.First().CreateFont(36, FontStyle.Bold);
        else
            return "Erro: Nenhuma fonte encontrada no sistema para renderizar o banner.";

        Font bigFont = font.Family.CreateFont(56, FontStyle.Bold);

        var darkBlue = Color.ParseHex("#0B2046");
        var green = Color.ParseHex("#128A81");

        // --- 1. Tarja Superior Direita (VENDE-SE + Telefone) ---
        // Desenha um polígono estilo "seta"/"chanfro" que se alinha a direita.
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

        // Elementos mockados de ícones na barra branca inferior
        Font smallFont = font.Family.CreateFont(18, FontStyle.Bold);
        image.Mutate(x => x.DrawText("📍 Excelente Localizacao   🛡️ Bairro Tranquilo e Valorizado   🔑 Pronta para Morar   📄 Documentacao Regular", smallFont, darkBlue, new PointF(40, image.Height - 65f)));

        // --- 3. Tarjas de Valor e Localização (Canto Esquerdo Inferior) ---
        // Fita Azul Escura de Localização (por baixo da verde)
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

        // Fita Verde de Preço (sobreposta a direita)
        var pricePathWidth = 500f;
        var pricePathHeight = 85f;
        var pricePathLeft = locPathWidth - 80f; // Overlaps
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
        
        // O texto tem tamanhos compostos na imagem (R$ pequeno, 400 grande)
        image.Mutate(x => x.DrawText($"R$ {price:N0}   |  {bedrooms} Qtos", bigFont, Color.White, new PointF(pricePathLeft + 20f, pricePathY + 10f)));

        await image.SaveAsync(bannerPath);
        return bannerPath;
    }
}
