using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Services.FastPath
{
  public class ConversationalFastPathInterceptor : IFastPathInterceptor
  {
    // Expressão regular compilada para máxima performance. 
    // Detecta padrões estritos de saudação sem comandos adicionais atrelados.
    private static readonly Regex GreetingRegex = new(
        @"^(oi|ol[áa]|bom dia|boa tarde|boa noite|tudo bem\??|hello|hi|eae|e a[íi])\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<(bool IsFastPath, string? Response)> EvaluateAsync(string input, CancellationToken cancellationToken = default)
    {
      if (string.IsNullOrWhiteSpace(input))
        return Task.FromResult<(bool IsFastPath, string? Response)>((false, null));

      var normalizedInput = input.Trim();

      // Heurística de segurança: se a mensagem tem mais de 40 caracteres, não é uma saudação simples e requer orquestração
      if (normalizedInput.Length > 40)
        return Task.FromResult<(bool IsFastPath, string? Response)>((false, null));

      if (GreetingRegex.IsMatch(normalizedInput))
      {
        var response = GetDynamicGreeting(normalizedInput);
        return Task.FromResult<(bool IsFastPath, string? Response)>((true, response));
      }

      return Task.FromResult<(bool IsFastPath, string? Response)>((false, null));
    }

    private static string GetDynamicGreeting(string input)
    {
      var lowerInput = input.ToLowerInvariant();

      if (lowerInput.Contains("bom dia")) return "Bom dia! Como posso ajudar você hoje?";
      if (lowerInput.Contains("boa tarde")) return "Boa tarde! Como posso ajudar?";
      if (lowerInput.Contains("boa noite")) return "Boa noite! Em que posso ser útil?";
      if (lowerInput.Contains("tudo bem")) return "Tudo bem por aqui! E com você? Como posso te ajudar agora?";

      return "Olá! Como posso ajudar você hoje?";
    }
  }
}