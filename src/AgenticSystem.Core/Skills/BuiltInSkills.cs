using AgenticSystem.Core.Interfaces;

namespace AgenticSystem.Core.Skills;

public class CodingAssistantSkill : ISkill
{
    public string Id => "coding-assistant";
    public string Name => "Coding Assistant";
    public string Domain => "work";
    public SkillType Type => SkillType.Instruction;

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = @"You are an expert software engineer.
When helping with code:
- Follow Clean Code principles
- Suggest tests alongside implementation
- Use idiomatic patterns for the target language
- Point out potential security issues
- Prefer composition over inheritance",
            FewShotExamples = @"User: How do I create a REST endpoint in C#?
Assistant: Here's a minimal API endpoint in .NET 10:

```csharp
app.MapGet(""/api/items/{id}"", async (int id, IItemService service) =>
{
    var item = await service.GetByIdAsync(id);
    return item is not null ? Results.Ok(item) : Results.NotFound();
});
```",
            Metadata = new Dictionary<string, string>
            {
                ["languages"] = "csharp,java,python,typescript",
                ["frameworks"] = "dotnet,spring,fastapi,react"
            }
        });
    }
}

public class ProductivitySkill : ISkill
{
    public string Id => "productivity";
    public string Name => "Productivity & Planning";
    public string Domain => "personal";
    public SkillType Type => SkillType.Instruction;

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = @"You are a productivity coach.
When helping with planning and organization:
- Break tasks into actionable steps (max 30 min each)
- Use timeboxing for focus work
- Suggest priority using Eisenhower Matrix (Urgent/Important)
- Recommend review/retrospective at end of day
- Track progress with simple metrics",
            Metadata = new Dictionary<string, string>
            {
                ["methods"] = "pomodoro,eisenhower,gtd,kanban"
            }
        });
    }
}

public class CreativeWritingSkill : ISkill
{
    public string Id => "creative-writing";
    public string Name => "Creative Writing";
    public string Domain => "creative";
    public SkillType Type => SkillType.Knowledge;

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = @"You are a creative writing assistant.
When helping with writing:
- Adapt tone and style to the target audience
- Use active voice and strong verbs
- Show, don't tell
- Structure with clear beginning, middle, end
- Offer alternative phrasings when asked",
            Metadata = new Dictionary<string, string>
            {
                ["styles"] = "narrative,technical,persuasive,descriptive"
            }
        });
    }
}

public class DataAnalysisSkill : ISkill
{
    public string Id => "data-analysis";
    public string Name => "Data Analysis";
    public string Domain => "analysis";
    public SkillType Type => SkillType.Knowledge;

    public Task<SkillContent> GetContentAsync(SkillContext context)
    {
        return Task.FromResult(new SkillContent
        {
            SystemPromptFragment = @"You are a data analysis expert.
When helping with data:
- Identify patterns and outliers
- Suggest appropriate visualizations
- Use statistical methods when relevant
- Present findings with confidence intervals
- Recommend next steps based on data insights",
            Metadata = new Dictionary<string, string>
            {
                ["tools"] = "sql,pandas,excel,powerbi",
                ["methods"] = "regression,clustering,timeseries"
            }
        });
    }
}
