using System;
using Microsoft.ML.Data;

namespace AgenticSystem.Core.Services.FastPath
{
  public class FastPathModelInput
  {
    [LoadColumn(0)]
    public string Text { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string Label { get; set; } = string.Empty;
  }


  public class FastPathModelOutput
  {
    [ColumnName("PredictedLabel")]
    public string Intent { get; set; } = string.Empty;

    public float[] Score { get; set; } = Array.Empty<float>();
  }
}