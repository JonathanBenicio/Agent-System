using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML;
using AgenticSystem.Core.Services.FastPath;

namespace AgenticSystem.Infrastructure.Scripts
{
    public class LabeledData
    {
        public string Text { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public static class FastPathModelGenerator
    {
        public static void Generate(string outputPath = "fastpath_model.zip")
        {
            var mlContext = new MLContext(seed: 1);

            // 1. Dados de treinamento simples
            var rawData = new List<string>
            {
                "Oi", "Olá", "Bom dia", "Boa tarde", "Boa noite", "Hello", "Hi",
                "Tudo bem?", "Como vai?",
                "Obrigado", "Valeu", "Thanks"
            };

            var data = new List<LabeledData>();
            foreach (var text in rawData)
            {
                string label = "Greeting";
                if (text.Contains("Tudo bem") || text.Contains("Como vai")) label = "SmallTalk_HowAreYou";
                if (text.Contains("Obrigado") || text.Contains("Valeu") || text.Contains("Thanks")) label = "SmallTalk_Thanks";
                
                data.Add(new LabeledData { Text = text, Label = label });
            }

            var trainData = mlContext.Data.LoadFromEnumerable(data);

            // 3. Criar pipeline de treinamento
            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(mlContext.Transforms.Text.FeaturizeText("Features", "Text"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel")); // Maps PredictedLabel (key) to PredictedLabel (value)

            // 4. Treinar o modelo
            Console.WriteLine("Treinando modelo ML.NET...");
            var model = pipeline.Fit(trainData);

            // Diagnóstico de esquema
            var schema = model.GetOutputSchema(trainData.Schema);
            Console.WriteLine("Colunas de saída do modelo:");
            foreach (var col in schema)
            {
                Console.WriteLine($"- {col.Name} ({col.Type})");
            }

            // 4. Salvar
            if (File.Exists(outputPath)) File.Delete(outputPath);
            mlContext.Model.Save(model, trainData.Schema, outputPath);
            
            Console.WriteLine($"✅ Modelo gerado com sucesso em: {Path.GetFullPath(outputPath)}");
        }
    }
}

