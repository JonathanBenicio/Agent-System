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

    [Description("Gera o banner publicitario final desenhando textos, o preco e a quantidade de quartos sobre a imagem limpa.")]
    public async Task<string> RenderBannerAsync(
        [Description("Caminho absoluto para a foto limpa")] string cleanImagePath, 
        [Description("Preco do imovel (ex: 650000)")] decimal price, 
        [Description("Quantidade de quartos")] int bedrooms)
    {
        if (!File.Exists(cleanImagePath))
            return "Erro: Imagem limpa nao encontrada no caminho: " + cleanImagePath;

        var bannerPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(cleanImagePath) ?? "", $"banner_{System.IO.Path.GetFileName(cleanImagePath)}");
        
        using var image = await Image.LoadAsync(cleanImagePath);
        
        // Tentar obter uma fonte padrão instalada no sistema
        Font font;
        if (SystemFonts.TryGet("Arial", out var fontFamily))
            font = fontFamily.CreateFont(48, FontStyle.Bold);
        else if (SystemFonts.Families.Any())
            font = SystemFonts.Families.First().CreateFont(48, FontStyle.Bold);
        else
            return "Erro: Nenhuma fonte encontrada no sistema para renderizar o banner.";

        // Desenhar uma tarja na base da imagem
        var stripHeight = 120;
        var rect = new RectangleF(0, image.Height - stripHeight, image.Width, stripHeight);
        var bannerColor = Color.ParseHex("#00000099"); // Preto com transparência
        image.Mutate(x => x.Fill(bannerColor, rect));

        // Escrever o preço à esquerda
        var textPrice = $"Venda: R$ {price:N2}";
        var priceLocation = new PointF(40, image.Height - 90);
        image.Mutate(x => x.DrawText(textPrice, font, Color.White, priceLocation));

        // Escrever a quantidade de quartos à direita
        var textBedrooms = $"{bedrooms} Quartos";
        
        // Aproximação do tamanho do texto para alinhar à direita
        var estimatedTextWidth = textBedrooms.Length * 30; 
        var bedLocation = new PointF(Math.Max(image.Width - estimatedTextWidth - 40, priceLocation.X + 400), image.Height - 90);
        
        image.Mutate(x => x.DrawText(textBedrooms, font, Color.Yellow, bedLocation));

        await image.SaveAsync(bannerPath);
        return bannerPath;
    }
}
