using System;
using System.Collections.Generic;
using System.IO;
using AgenticSystem.Core.Services.FastPath;
using Microsoft.ML;

namespace AgenticSystem.FastPathTrainer;

class Program
{
    static void Main(string[] args)
    {
        var mlContext = new MLContext(seed: 0);

        // 1. Prepare sample data (Enhanced with more variations and new categories)
        var trainingData = new List<FastPathModelInput>
        {
            // Greetings
            new() { Text = "Oi", Label = "Greeting" },
            new() { Text = "Olá", Label = "Greeting" },
            new() { Text = "Bom dia", Label = "Greeting" },
            new() { Text = "Boa tarde", Label = "Greeting" },
            new() { Text = "Boa noite", Label = "Greeting" },
            new() { Text = "Hey", Label = "Greeting" },
            new() { Text = "Hello", Label = "Greeting" },
            new() { Text = "Opa", Label = "Greeting" },
            new() { Text = "Salve", Label = "Greeting" },
            new() { Text = "Coé", Label = "Greeting" },
            new() { Text = "Hi", Label = "Greeting" },

            // SmallTalk_HowAreYou
            new() { Text = "Como você está?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "Tudo bem com você?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "Como vai?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "Tudo certo?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "Como você tem passado?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "How are you?", Label = "SmallTalk_HowAreYou" },
            new() { Text = "Tudo em ordem?", Label = "SmallTalk_HowAreYou" },

            // SmallTalk_Thanks
            new() { Text = "Obrigado", Label = "SmallTalk_Thanks" },
            new() { Text = "Valeu", Label = "SmallTalk_Thanks" },
            new() { Text = "Muito obrigado", Label = "SmallTalk_Thanks" },
            new() { Text = "Thanks", Label = "SmallTalk_Thanks" },
            new() { Text = "Muitíssimo obrigado", Label = "SmallTalk_Thanks" },
            new() { Text = "Agradecido", Label = "SmallTalk_Thanks" },
            new() { Text = "Show, valeu", Label = "SmallTalk_Thanks" },

            // Agent_Capabilities
            new() { Text = "O que você pode fazer?", Label = "Agent_Capabilities" },
            new() { Text = "Quais são suas funções?", Label = "Agent_Capabilities" },
            new() { Text = "Quem é você?", Label = "Agent_Capabilities" },
            new() { Text = "Me ajude a entender seus comandos", Label = "Agent_Capabilities" },
            new() { Text = "O que você faz?", Label = "Agent_Capabilities" },
            new() { Text = "Quais seus poderes?", Label = "Agent_Capabilities" },
            new() { Text = "Quais ferramentas você tem?", Label = "Agent_Capabilities" },
            new() { Text = "What can you do?", Label = "Agent_Capabilities" },

            // System_Status
            new() { Text = "Como está o sistema?", Label = "System_Status" },
            new() { Text = "Status do sistema", Label = "System_Status" },
            new() { Text = "Está tudo online?", Label = "System_Status" },
            new() { Text = "Health check", Label = "System_Status" },
            new() { Text = "O sistema está funcionando?", Label = "System_Status" },
            new() { Text = "Status report", Label = "System_Status" },
            new() { Text = "Como está a saúde da aplicação?", Label = "System_Status" },

            // Goodbye
            new() { Text = "Tchau", Label = "Goodbye" },
            new() { Text = "Até logo", Label = "Goodbye" },
            new() { Text = "Sair", Label = "Goodbye" },
            new() { Text = "Bye", Label = "Goodbye" },
            new() { Text = "Até mais", Label = "Goodbye" },
            new() { Text = "Fui", Label = "Goodbye" },
            new() { Text = "Até amanhã", Label = "Goodbye" },
            new() { Text = "Goodbye", Label = "Goodbye" },

            // Feedback_Positive
            new() { Text = "Bom trabalho", Label = "Feedback_Positive" },
            new() { Text = "Gostei muito", Label = "Feedback_Positive" },
            new() { Text = "Excelente", Label = "Feedback_Positive" },
            new() { Text = "Top", Label = "Feedback_Positive" },
            new() { Text = "Legal", Label = "Feedback_Positive" },
            new() { Text = "Perfeito", Label = "Feedback_Positive" },
            new() { Text = "Ajudou muito", Label = "Feedback_Positive" },
            new() { Text = "Good job", Label = "Feedback_Positive" },

            // Feedback_Negative
            new() { Text = "Isso está errado", Label = "Feedback_Negative" },
            new() { Text = "Não gostei", Label = "Feedback_Negative" },
            new() { Text = "Ruim", Label = "Feedback_Negative" },
            new() { Text = "Terrível", Label = "Feedback_Negative" },
            new() { Text = "Péssimo", Label = "Feedback_Negative" },
            new() { Text = "Não ajudou", Label = "Feedback_Negative" },
            new() { Text = "Horrível", Label = "Feedback_Negative" },

            // User_Help
            new() { Text = "Ajuda", Label = "User_Help" },
            new() { Text = "Help", Label = "User_Help" },
            new() { Text = "Como usar?", Label = "User_Help" },
            new() { Text = "Manual", Label = "User_Help" },
            new() { Text = "Me ensina", Label = "User_Help" },
            new() { Text = "Quais comandos?", Label = "User_Help" },
            new() { Text = "Socorro", Label = "User_Help" }
        };

        var dataView = mlContext.Data.LoadFromEnumerable(trainingData);

        // 2. Build pipeline (Multiclass Classification) - Correct schema for ONNX
        var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label")
            .Append(mlContext.Transforms.Text.TokenizeIntoWords("Tokens", "Text"))
            .Append(mlContext.Transforms.Conversion.MapValueToKey("TokenKeys", "Tokens"))
            .Append(mlContext.Transforms.Text.ProduceNgrams("Features", "TokenKeys"))
            .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
            .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        // 3. Train model
        Console.WriteLine("Training optimized Multiclass Model...");
        var model = pipeline.Fit(dataView);

        // 4. Save model (Ensuring it goes to the project root)
        var modelPath = "fastpath_model.zip";
        Console.WriteLine($"Saving model to: {Path.GetFullPath(modelPath)}");

        
        mlContext.Model.Save(model, dataView.Schema, modelPath);

        // 5. Export to ONNX
        var onnxPath = "fastpath_model.onnx";
        Console.WriteLine($"Exporting model to ONNX: {Path.GetFullPath(onnxPath)}");
        
        using (var fileStream = File.Create(onnxPath))
        {
            mlContext.Model.ConvertToOnnx(model, dataView, fileStream);
        }

        Console.WriteLine("Model saved successfully in both ZIP and ONNX formats! Fast Path updated.");
    }
}

